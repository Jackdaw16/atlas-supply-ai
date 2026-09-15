using System.Text.Json;
using AtlasSupply.Application;
using Xunit;

namespace AtlasSupply.Agent.Tests;

public sealed class AgentServiceTests
{
    [Fact]
    public async Task ChatAsync_UsesRagAndReturnsSources()
    {
        var source = new KnowledgeSearchResult(
            "docs/knowledge/incident-escalation.md",
            "hash",
            2,
            "Escalation",
            "Escalate supplier delays within one business day.",
            0.14);
        var languageModel = new ScriptedLanguageModel(
            ToolCall("search_knowledge", "{\"query\":\"incident escalation\",\"topK\":2}"),
            Final("Escalate the incident within one business day."));
        var retrieval = new FakeKnowledgeRetrievalService(source);
        var session = new FakeToolSession();
        var service = CreateService(languageModel, session, retrieval);

        var result = await service.ChatAsync(new AgentChatRequest("How do I escalate an incident?"), CancellationToken.None);

        Assert.Equal("Escalate the incident within one business day.", result.Message);
        Assert.Equal(["search_knowledge"], result.ToolsUsed);
        Assert.Equal([source], result.RagSources);
        Assert.Equal("incident escalation", retrieval.Inputs.Single().Query);
        Assert.Equal(2, retrieval.Inputs.Single().TopK);
        Assert.Empty(session.Invocations);
    }

    [Fact]
    public async Task ChatAsync_UsesDiscoveredMcpTool()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            "get_delayed_orders",
            "Lists approved purchase orders that have not been received yet.",
            EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall("get_delayed_orders", "{}"),
                Final("There is one delayed order.")),
            session,
            new FakeKnowledgeRetrievalService());

        var result = await service.ChatAsync(new AgentChatRequest("What orders are delayed?"), CancellationToken.None);

        Assert.Equal("There is one delayed order.", result.Message);
        Assert.Equal(["get_delayed_orders"], result.ToolsUsed);
        Assert.Empty(result.RagSources);
        Assert.Single(session.Invocations);
        Assert.Equal("get_delayed_orders", session.Invocations.Single().ToolName);
    }

    [Fact]
    public async Task ChatAsync_UsesRagAndMcpToolsInReportedOrder()
    {
        var source = new KnowledgeSearchResult("docs/knowledge/purchasing.md", "hash", 0, "Rules", "Policy", 0.09);
        var session = new FakeToolSession(new AgentToolDefinition(
            "list_suppliers",
            "Lists suppliers.",
            EmptyObjectSchema()));
        var languageModel = new ScriptedLanguageModel(
                new AgentLanguageModelResponse(string.Empty,
                [
                    new AgentToolCall("call-rag", "search_knowledge", "{\"query\":\"purchasing policy\"}"),
                    new AgentToolCall("call-mcp", "list_suppliers", "{}")
                ]),
                Final("The policy applies to the listed supplier."));
        var service = CreateService(
            languageModel,
            session,
            new FakeKnowledgeRetrievalService(source));

        var result = await service.ChatAsync(new AgentChatRequest("Which suppliers meet the purchasing policy?"), CancellationToken.None);

        Assert.Equal(["search_knowledge", "list_suppliers"], result.ToolsUsed);
        Assert.Equal([source], result.RagSources);
        Assert.Equal("list_suppliers", session.Invocations.Single().ToolName);
        var assistantMessage = Assert.IsType<AgentAssistantMessage>(languageModel.Requests[1].Messages[1]);
        Assert.Equal(["call-rag", "call-mcp"], assistantMessage.ToolCalls.Select(call => call.Id));
        Assert.Collection(languageModel.Requests[1].Messages.Skip(2),
            message => Assert.Equal("call-rag", Assert.IsType<AgentToolMessage>(message).ToolCallId),
            message => Assert.Equal("call-mcp", Assert.IsType<AgentToolMessage>(message).ToolCallId));
    }

    [Fact]
    public async Task ChatAsync_ForwardsCreateIncidentArgumentsToMcp()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            "create_incident",
            "Creates an incident.",
            EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(
                    "create_incident",
                    "{\"type\":\"Delay\",\"description\":\"Shipment is late\",\"supplierId\":\"ea3ed268-776f-4da8-86bb-6d2502f00982\"}"),
                Final("The incident was created.")),
            session,
            new FakeKnowledgeRetrievalService());

        await service.ChatAsync(new AgentChatRequest("Create a delay incident."), CancellationToken.None);

        var invocation = session.Invocations.Single();
        Assert.Equal("create_incident", invocation.ToolName);
        Assert.Equal("Delay", invocation.Arguments.GetProperty("type").GetString());
        Assert.Equal("Shipment is late", invocation.Arguments.GetProperty("description").GetString());
        Assert.Equal("ea3ed268-776f-4da8-86bb-6d2502f00982", invocation.Arguments.GetProperty("supplierId").GetString());
    }

    [Fact]
    public async Task ChatAsync_FailsAfterConfiguredToolRoundLimit()
    {
        var languageModel = new ScriptedLanguageModel(
            ToolCall("search_knowledge", "{\"query\":\"policy\"}"),
            ToolCall("create_incident", "{}"));
        var retrieval = new FakeKnowledgeRetrievalService();
        var session = new FakeToolSession(new AgentToolDefinition(
            "create_incident", "Creates an incident.", EmptyObjectSchema()));
        var service = CreateService(
            languageModel,
            session,
            retrieval,
            new AgentServiceOptions(MaximumToolRounds: 1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChatAsync(new AgentChatRequest("Keep looking."), CancellationToken.None));

        Assert.Equal("Agent exceeded the configured maximum of 1 tool rounds.", exception.Message);
        Assert.Equal(2, languageModel.Requests.Count);
        Assert.Single(retrieval.Inputs);
        Assert.Empty(session.Invocations);
    }

    [Fact]
    public async Task ChatAsync_ReturnsLocalRagArgumentErrorsToTheModel()
    {
        var languageModel = new ScriptedLanguageModel(
            ToolCall("search_knowledge", "{invalid"),
            Final("Please provide a valid knowledge search query."));
        var service = CreateService(languageModel, new FakeToolSession(), new FakeKnowledgeRetrievalService());

        var result = await service.ChatAsync(new AgentChatRequest("Find the policy."), CancellationToken.None);

        Assert.Equal("Please provide a valid knowledge search query.", result.Message);
        var toolResult = Assert.IsType<AgentToolMessage>(languageModel.Requests[1].Messages[^1]);
        Assert.True(toolResult.IsError);
        Assert.Contains("valid JSON", toolResult.Content);
    }

    private static AgentService CreateService(
        ScriptedLanguageModel languageModel,
        FakeToolSession toolSession,
        FakeKnowledgeRetrievalService retrieval,
        AgentServiceOptions? options = null)
    {
        return new AgentService(languageModel, new FakeToolProvider(toolSession), retrieval, options);
    }

    private static AgentLanguageModelResponse ToolCall(string name, string arguments) =>
        new(string.Empty, [new AgentToolCall($"call-{name}", name, arguments)]);

    private static AgentLanguageModelResponse Final(string message) => new(message, []);

    private static JsonElement EmptyObjectSchema()
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return document.RootElement.Clone();
    }

    private sealed class ScriptedLanguageModel(params AgentLanguageModelResponse[] responses) : IAgentLanguageModel
    {
        private readonly Queue<AgentLanguageModelResponse> _responses = new(responses);

        public List<AgentLanguageModelRequest> Requests { get; } = [];

        public Task<AgentLanguageModelResponse> CompleteAsync(
            AgentLanguageModelRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class FakeToolProvider(FakeToolSession session) : IAgentToolProvider
    {
        public Task<IAgentToolSession> OpenSessionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IAgentToolSession>(session);
    }

    private sealed class FakeToolSession(params AgentToolDefinition[] tools) : IAgentToolSession
    {
        public List<AgentToolInvocation> Invocations { get; } = [];

        public Task<IReadOnlyList<AgentToolDefinition>> DiscoverToolsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentToolDefinition>>(tools);

        public Task<AgentToolExecutionResult> InvokeAsync(
            AgentToolInvocation invocation,
            CancellationToken cancellationToken)
        {
            Invocations.Add(invocation);
            return Task.FromResult(new AgentToolExecutionResult("{\"ok\":true}", IsError: false));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeKnowledgeRetrievalService(params KnowledgeSearchResult[] results) : IKnowledgeRetrievalService
    {
        public List<KnowledgeSearchInput> Inputs { get; } = [];

        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            KnowledgeSearchInput input,
            CancellationToken cancellationToken)
        {
            Inputs.Add(input);
            return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>(results);
        }
    }
}
