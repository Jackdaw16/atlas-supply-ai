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

public sealed class AgentRoutingProviderException(
    int? statusCode,
    string? reasonPhrase,
    string? responseBody,
    Exception? innerException = null) : Exception(CreateMessage(statusCode, reasonPhrase), innerException)
{
    public int? StatusCode { get; } = statusCode;

    public string? ReasonPhrase { get; } = reasonPhrase;

    public string? ResponseBody { get; } = responseBody;

    private static string CreateMessage(int? statusCode, string? reasonPhrase)
    {
        if (statusCode is null)
        {
            return "Agent routing provider request failed.";
        }

        return string.IsNullOrWhiteSpace(reasonPhrase)
            ? $"Agent routing provider returned status {statusCode}."
            : $"Agent routing provider returned status {statusCode} ({reasonPhrase}).";
    }
}

public sealed class AgentRoutingResponseException(
    string fieldPath,
    string detail,
    Exception? innerException = null) : Exception(
        $"Agent routing response validation failed at '{fieldPath}': {detail}.",
        innerException)
{
    public string FieldPath { get; } = fieldPath;
}

public sealed record AgentRoutingShadowComparison(
    AgentRoutingDecision RoutingDecision,
    string? LanguageModelRoute,
    IReadOnlyList<string> LanguageModelToolNames,
    bool Comparable,
    bool? Matched);

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

public sealed record AgentServiceOptions(
    int MaximumToolRounds = 4,
    bool ExperimentalJevShadowRouting = false);

public sealed class AgentService(
    IAgentLanguageModel languageModel,
    IAgentToolProvider toolProvider,
    IKnowledgeRetrievalService knowledgeRetrievalService,
    IAgentToolAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<AgentService> logger,
    AgentServiceOptions? options = null,
    IAgentRouter? agentRouter = null)
{
    private const int MaximumMessageLength = 4_000;
    private const int MaximumToolCallsPerCompletion = 8;
    private const int MaximumProviderReasonPhraseLength = 160;
    private const int MaximumProviderResponseExcerptLength = 400;
    private static readonly string[] CredentialMarkers =
    [
        "authorization",
        "apikey",
        "token",
        "secret",
        "password",
        "credential"
    ];
    private static readonly EventId AgentRoutingShadowComparisonEventId =
        new(1, "AgentRoutingShadowComparison");
    private static readonly EventId AgentRoutingShadowFailureEventId =
        new(2, "AgentRoutingShadowFailure");

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

            if (toolRounds == 0)
            {
                await CompareInitialRoutingAsync(message, tools, completion, cancellationToken);
            }

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

    private async Task CompareInitialRoutingAsync(
        string message,
        IReadOnlyList<AgentToolDefinition> tools,
        AgentLanguageModelResponse completion,
        CancellationToken cancellationToken)
    {
        if (!_options.ExperimentalJevShadowRouting || tools.Count == 0 || agentRouter is null)
        {
            return;
        }

        try
        {
            var decision = await agentRouter.RouteAsync(
                new AgentRoutingRequest(message, tools.Select(static tool => tool.Name).ToArray()),
                cancellationToken);
            var comparison = CreateRoutingShadowComparison(decision, completion);

            logger.LogInformation(
                AgentRoutingShadowComparisonEventId,
                "Jev shadow comparison: Jev={JevRoute} ({JevProbability:P1}) OpenAI={LlmRoute} Tools={LlmToolNames} Comparable={Comparable} Matched={Matched} LatencyMs={JevElapsedMilliseconds} CostUsd={JevEstimatedCostUsd} JevProbabilities={JevProbabilities} LlmToolCount={LlmToolCount} JevInputTokens={JevInputTokens} JevOutputTokens={JevOutputTokens}",
                comparison.RoutingDecision.SelectedRoute,
                comparison.RoutingDecision.SelectedProbability,
                comparison.LanguageModelRoute,
                comparison.LanguageModelToolNames,
                comparison.Comparable,
                comparison.Matched,
                comparison.RoutingDecision.Telemetry.ElapsedMilliseconds,
                comparison.RoutingDecision.Telemetry.EstimatedCostUsd,
                comparison.RoutingDecision.ProbabilityDistribution,
                comparison.LanguageModelToolNames.Count,
                comparison.RoutingDecision.Telemetry.InputTokens,
                comparison.RoutingDecision.Telemetry.OutputTokens);
        }
        catch (Exception exception)
        {
            var innerException = exception.InnerException;
            var providerFailure = GetProviderFailureDetails(exception, message);
            logger.LogWarning(
                AgentRoutingShadowFailureEventId,
                "Agent routing shadow comparison failed {ShadowFailureType} {ExceptionType} {ExceptionMessage} {InnerExceptionType} {InnerExceptionMessage} {ProviderFailureKind} {ProviderStatusCode} {ProviderReasonPhrase} {ProviderResponseExcerpt}",
                GetShadowFailureType(exception),
                exception.GetType().Name,
                SanitizeProviderText(exception.Message, message, MaximumProviderReasonPhraseLength),
                innerException?.GetType().Name,
                SanitizeProviderText(innerException?.Message, message, MaximumProviderReasonPhraseLength),
                providerFailure.Kind,
                providerFailure.StatusCode,
                providerFailure.ReasonPhrase,
                providerFailure.ResponseExcerpt);
        }
    }

    private static string GetShadowFailureType(Exception exception) => exception switch
    {
        AgentRoutingProviderException => "ProviderFailure",
        AgentRoutingResponseException => "ResponseValidationFailure",
        ArgumentException => "RequestValidationFailure",
        _ => "UnknownFailure"
    };

    private static ProviderFailureDetails GetProviderFailureDetails(Exception exception, string userMessage)
    {
        if (exception is not AgentRoutingProviderException providerException)
        {
            return new ProviderFailureDetails(null, null, null, null);
        }

        return new ProviderFailureDetails(
            providerException.StatusCode is null ? "Transport" : "HttpResponse",
            providerException.StatusCode,
            SanitizeProviderText(
                providerException.ReasonPhrase,
                userMessage,
                MaximumProviderReasonPhraseLength),
            CreateProviderResponseExcerpt(providerException.ResponseBody, userMessage));
    }

    private static string? CreateProviderResponseExcerpt(string? responseBody, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("error", out var error))
            {
                return null;
            }

            var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (error.ValueKind == JsonValueKind.String)
            {
                AddSafeErrorField(fields, "message", error.GetString(), userMessage);
            }
            else if (error.ValueKind == JsonValueKind.Object)
            {
                AddSafeErrorField(error, fields, "message", userMessage);
                AddSafeErrorField(error, fields, "code", userMessage);
                AddSafeErrorField(error, fields, "type", userMessage);
            }
            else
            {
                return null;
            }

            if (fields.Count == 0)
            {
                return null;
            }

            return SanitizeProviderText(
                JsonSerializer.Serialize(fields),
                userMessage,
                MaximumProviderResponseExcerptLength);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AddSafeErrorField(
        JsonElement error,
        SortedDictionary<string, string> fields,
        string name,
        string userMessage)
    {
        if (!error.TryGetProperty(name, out var value))
        {
            return;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
        AddSafeErrorField(fields, name, text, userMessage);
    }

    private static void AddSafeErrorField(
        SortedDictionary<string, string> fields,
        string name,
        string? value,
        string userMessage)
    {
        var sanitizedValue = SanitizeProviderText(value, userMessage, MaximumProviderResponseExcerptLength);
        if (sanitizedValue is not null)
        {
            fields[name] = sanitizedValue;
        }
    }

    private static string? SanitizeProviderText(string? value, string userMessage, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = string.Join(" ", new string(value
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (sanitized.Length == 0 ||
            (!string.IsNullOrEmpty(userMessage) &&
             sanitized.Contains(userMessage, StringComparison.OrdinalIgnoreCase)) ||
            CredentialMarkers.Any(marker =>
                sanitized.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return sanitized.Length <= maximumLength
            ? sanitized
            : sanitized[..maximumLength];
    }

    private sealed record ProviderFailureDetails(
        string? Kind,
        int? StatusCode,
        string? ReasonPhrase,
        string? ResponseExcerpt);

    private static AgentRoutingShadowComparison CreateRoutingShadowComparison(
        AgentRoutingDecision decision,
        AgentLanguageModelResponse completion)
    {
        var toolNames = completion.ToolCalls.Select(static toolCall => toolCall.Name).ToArray();
        var languageModelRoute = toolNames.Length switch
        {
            0 => AgentRoute.General,
            1 => toolNames[0],
            _ => null
        };
        var comparable = toolNames.Length <= 1;

        return new AgentRoutingShadowComparison(
            decision,
            languageModelRoute,
            toolNames,
            comparable,
            comparable
                ? string.Equals(decision.SelectedRoute, languageModelRoute, StringComparison.Ordinal)
                : null);
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
