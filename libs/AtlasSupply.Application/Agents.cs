using System.Text.Json;
using AtlasSupply.Domain;
using Microsoft.Extensions.Logging;

namespace AtlasSupply.Application;

public sealed record AgentChatRequest(string Message);

public sealed record AgentChatResult(
    string Message,
    IReadOnlyList<string> ToolsUsed,
    IReadOnlyList<KnowledgeSearchResult> RagSources);

public sealed record AgentToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema);

public sealed record AgentToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

public sealed record AgentToolInvocation(
    string CallId,
    string ToolName,
    JsonElement Arguments);

public sealed record AgentToolExecutionResult(string Content, bool IsError);

public abstract record AgentMessage;

public sealed record AgentUserMessage(string Content) : AgentMessage;

public sealed record AgentAssistantMessage(
    string Content,
    IReadOnlyList<AgentToolCall> ToolCalls) : AgentMessage;

public sealed record AgentToolMessage(
    string ToolCallId,
    string ToolName,
    string Content,
    bool IsError) : AgentMessage;

public sealed record AgentLanguageModelRequest(
    string SystemInstructions,
    IReadOnlyList<AgentMessage> Messages,
    IReadOnlyList<AgentToolDefinition> Tools);

public sealed record AgentLanguageModelResponse(
    string Content,
    IReadOnlyList<AgentToolCall> ToolCalls);

public static class AgentRoute
{
    public const string General = "general";
}

public sealed record AgentRoutingRequest(
    string Message,
    IReadOnlyList<string> AvailableTools);

public sealed record AgentRoutingTelemetry(
    string? Provider,
    string? Model,
    int? InputTokens,
    int? OutputTokens,
    decimal? EstimatedCostUsd,
    long ElapsedMilliseconds);

public sealed record AgentRoutingDecision(
    string SelectedRoute,
    decimal SelectedProbability,
    IReadOnlyDictionary<string, decimal> ProbabilityDistribution,
    AgentRoutingTelemetry Telemetry);

public interface IAgentRouter
{
    Task<AgentRoutingDecision> RouteAsync(
        AgentRoutingRequest request,
        CancellationToken cancellationToken);
}

public interface IAgentLanguageModel
{
    Task<AgentLanguageModelResponse> CompleteAsync(
        AgentLanguageModelRequest request,
        CancellationToken cancellationToken);
}

public interface IAgentToolProvider
{
    Task<IAgentToolSession> OpenSessionAsync(CancellationToken cancellationToken);
}

public interface IAgentToolSession : IAsyncDisposable
{
    Task<IReadOnlyList<AgentToolDefinition>> DiscoverToolsAsync(CancellationToken cancellationToken);

    Task<AgentToolExecutionResult> InvokeAsync(
        AgentToolInvocation invocation,
        CancellationToken cancellationToken);
}

public sealed record AgentServiceOptions(int MaximumToolRounds = 4);

public sealed class AgentService(
    IAgentLanguageModel languageModel,
    IAgentToolProvider toolProvider,
    IKnowledgeRetrievalService knowledgeRetrievalService,
    IAgentToolAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<AgentService> logger,
    AgentServiceOptions? options = null)
{
    private const int MaximumMessageLength = 4_000;
    private const int MaximumToolCallsPerCompletion = 8;

    public const string SystemInstructions =
        "Use RAG for Atlas Supply policies and procedures. Use MCP for live transactional data and actions. Never invent suppliers, orders, incidents, or company policies. Use tools rather than assumptions when authoritative data exists.";

    private readonly AgentServiceOptions _options = ValidateOptions(options ?? new AgentServiceOptions());

    public async Task<AgentChatResult> ChatAsync(
        AgentChatRequest request,
        AgentAuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var message = ValidateMessage(request);
        ArgumentNullException.ThrowIfNull(authorizationContext);

        await using var toolSession = await toolProvider.OpenSessionAsync(cancellationToken);
        var discoveredTools = await toolSession.DiscoverToolsAsync(cancellationToken);
        var tools = CreateToolCatalog(discoveredTools, authorizationContext);
        var toolsByName = tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var systemInstructions = CreateSystemInstructions(authorizationContext);
        var messages = new List<AgentMessage> { new AgentUserMessage(message) };
        var toolsUsed = new List<string>();
        var ragSources = new List<KnowledgeSearchResult>();

        for (var toolRounds = 0; ;)
        {
            var completion = await languageModel.CompleteAsync(
                new AgentLanguageModelRequest(systemInstructions, messages, tools),
                cancellationToken);

            if (completion.ToolCalls.Count == 0)
            {
                return new AgentChatResult(completion.Content, toolsUsed, ragSources);
            }

            if (toolRounds >= _options.MaximumToolRounds)
            {
                throw new InvalidOperationException(
                    $"Agent exceeded the configured maximum of {_options.MaximumToolRounds} tool rounds.");
            }

            if (completion.ToolCalls.Count > MaximumToolCallsPerCompletion)
            {
                throw new InvalidOperationException(
                    $"Agent requested more than {MaximumToolCallsPerCompletion} tool calls in one completion.");
            }

            messages.Add(new AgentAssistantMessage(completion.Content, completion.ToolCalls));

            foreach (var toolCall in completion.ToolCalls)
            {
                if (!AgentToolCapabilityMap.TryGetRequiredCapability(toolCall.Name, out var requiredCapability) ||
                    !authorizationContext.HasCapability(requiredCapability))
                {
                    if (requiredCapability is not null)
                    {
                        await WriteAuditAttemptAsync(
                            authorizationContext,
                            toolCall.Name,
                            requiredCapability,
                            authorized: false,
                            outcome: "AuthorizationDenied",
                            cancellationToken);
                    }

                    messages.Add(new AgentToolMessage(
                        toolCall.Id,
                        toolCall.Name,
                        CreateAuthorizationDeniedResult().Content,
                        IsError: true));
                    continue;
                }

                var auditRecord = await WriteAuditAttemptAsync(
                    authorizationContext,
                    toolCall.Name,
                    requiredCapability,
                    authorized: true,
                    outcome: "ExecutionStarted",
                    cancellationToken);

                if (!toolsByName.ContainsKey(toolCall.Name))
                {
                    auditRecord.Complete(succeeded: false, "ToolUnavailable");
                    await FinalizeAuditAsync(auditRecord, cancellationToken);
                    throw new InvalidOperationException($"Agent requested unavailable tool '{toolCall.Name}'.");
                }

                AgentToolExecutionResult result;
                try
                {
                    if (toolCall.Name == AgentToolCapabilityMap.SearchKnowledgeToolName)
                    {
                        result = await SearchKnowledgeAsync(toolCall, ragSources, cancellationToken);
                    }
                    else
                    {
                        var arguments = ParseArguments(toolCall);
                        result = await toolSession.InvokeAsync(
                            new AgentToolInvocation(toolCall.Id, toolCall.Name, arguments),
                            cancellationToken);
                    }
                }
                catch
                {
                    auditRecord.Complete(succeeded: false, "ToolExecutionFailed");
                    await FinalizeAuditAsync(auditRecord, cancellationToken);
                    throw;
                }

                auditRecord.Complete(
                    succeeded: !result.IsError,
                    result.IsError ? "ToolExecutionFailed" : "Succeeded");
                await FinalizeAuditAsync(auditRecord, cancellationToken);

                toolsUsed.Add(toolCall.Name);
                messages.Add(new AgentToolMessage(toolCall.Id, toolCall.Name, result.Content, result.IsError));
            }

            toolRounds++;
        }
    }

    private static AgentServiceOptions ValidateOptions(AgentServiceOptions options)
    {
        if (options.MaximumToolRounds is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaximumToolRounds,
                "Agent maximum tool rounds must be between 1 and 8.");
        }

        return options;
    }

    private static string ValidateMessage(AgentChatRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Chat message is required.", nameof(request));
        }

        var message = request.Message.Trim();
        if (message.Length > MaximumMessageLength)
        {
            throw new ArgumentException(
                $"Chat message cannot exceed {MaximumMessageLength} characters.",
                nameof(request));
        }

        return message;
    }

    private static IReadOnlyList<AgentToolDefinition> CreateToolCatalog(
        IReadOnlyList<AgentToolDefinition> discoveredTools,
        AgentAuthorizationContext authorizationContext)
    {
        ArgumentNullException.ThrowIfNull(discoveredTools);
        ArgumentNullException.ThrowIfNull(authorizationContext);

        var tools = new List<AgentToolDefinition>(discoveredTools.Count + 1)
        {
            new(
                AgentToolCapabilityMap.SearchKnowledgeToolName,
                "Searches Atlas Supply policies and procedures and returns source metadata.",
                CreateSearchKnowledgeSchema())
        };

        tools = tools
            .Where(tool => AgentToolCapabilityMap.IsAuthorized(tool.Name, authorizationContext))
            .ToList();
        tools.AddRange(discoveredTools.Where(tool =>
            AgentToolCapabilityMap.IsAuthorized(tool.Name, authorizationContext)));

        var duplicates = tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => string.IsNullOrWhiteSpace(group.Key) ? "(empty)" : group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Agent tool discovery returned duplicate or invalid tool names: {string.Join(", ", duplicates)}.");
        }

        return tools;
    }

    private static string CreateSystemInstructions(AgentAuthorizationContext authorizationContext) =>
        $"{SystemInstructions} {AgentToolCapabilityMap.CreateCapabilitySummary(authorizationContext)}";

    private static AgentToolExecutionResult CreateAuthorizationDeniedResult() =>
        new(JsonSerializer.Serialize(new { error = "Tool is not authorized." }), IsError: true);

    private async Task<AgentToolAuditRecord> WriteAuditAttemptAsync(
        AgentAuthorizationContext authorizationContext,
        string toolName,
        AgentCapabilityScope requiredCapability,
        bool authorized,
        string outcome,
        CancellationToken cancellationToken)
    {
        var auditRecord = new AgentToolAuditRecord(
            authorizationContext.UserId,
            authorizationContext.Username,
            toolName,
            requiredCapability.Value,
            authorized,
            succeeded: false,
            timeProvider.GetUtcNow().UtcDateTime,
            outcome);
        try
        {
            await auditWriter.WriteAttemptAsync(auditRecord, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new AgentToolAuditException("Unable to persist the agent tool audit record.", exception);
        }

        return auditRecord;
    }

    private async Task FinalizeAuditAsync(
        AgentToolAuditRecord auditRecord,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditWriter.WriteOutcomeAsync(auditRecord, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to finalize the audit record {AuditRecordId} for tool {ToolName}.",
                auditRecord.Id,
                auditRecord.ToolName);
        }
    }

    private static JsonElement CreateSearchKnowledgeSchema()
    {
        using var document = JsonDocument.Parse("""
            {"type":"object","properties":{"query":{"type":"string"},"topK":{"type":"integer","minimum":1,"maximum":20}},"required":["query"],"additionalProperties":false}
            """);
        return document.RootElement.Clone();
    }

    private static JsonElement ParseArguments(AgentToolCall toolCall)
    {
        if (string.IsNullOrWhiteSpace(toolCall.Id))
        {
            throw new ArgumentException("Agent tool call id is required.", nameof(toolCall));
        }

        try
        {
            using var document = JsonDocument.Parse(toolCall.ArgumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Agent tool arguments must be a JSON object.", nameof(toolCall));
            }

            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Agent tool arguments must be valid JSON.", nameof(toolCall), exception);
        }
    }

    private async Task<AgentToolExecutionResult> SearchKnowledgeAsync(
        AgentToolCall toolCall,
        List<KnowledgeSearchResult> ragSources,
        CancellationToken cancellationToken)
    {
        JsonElement arguments;
        try
        {
            arguments = ParseArguments(toolCall);
        }
        catch (ArgumentException exception)
        {
            return new AgentToolExecutionResult(
                JsonSerializer.Serialize(new { error = exception.Message }),
                IsError: true);
        }

        if (!arguments.TryGetProperty("query", out var query) || query.ValueKind != JsonValueKind.String)
        {
            return new AgentToolExecutionResult(
                JsonSerializer.Serialize(new { error = "search_knowledge requires a string query." }),
                IsError: true);
        }

        var topK = 5;
        if (arguments.TryGetProperty("topK", out var topKProperty) && !topKProperty.TryGetInt32(out topK))
        {
            return new AgentToolExecutionResult(
                JsonSerializer.Serialize(new { error = "search_knowledge topK must be an integer." }),
                IsError: true);
        }

        var results = await knowledgeRetrievalService.SearchAsync(
            new KnowledgeSearchInput(query.GetString()!, topK),
            cancellationToken);
        ragSources.AddRange(results);

        return new AgentToolExecutionResult(
            JsonSerializer.Serialize(new { sources = results }),
            IsError: false);
    }
}
