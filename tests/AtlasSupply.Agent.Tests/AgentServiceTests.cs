using System.Text.Json;
using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasSupply.Agent.Tests;

public sealed class AgentServiceTests
{
    private static readonly Guid ReadOnlyUserId = Guid.Parse("77777777-7777-7777-7777-777777777701");
    private static readonly Guid OperatorUserId = Guid.Parse("77777777-7777-7777-7777-777777777702");

    private static readonly AgentAuthorizationContext ReadOnlyAuthorization = new(
        ReadOnlyUserId,
        "readonly-user",
    [
        AgentCapabilityScope.KnowledgeSearch,
        AgentCapabilityScope.SuppliersList,
        AgentCapabilityScope.SuppliersRead,
        AgentCapabilityScope.OrdersDelayedRead
    ]);

    private static readonly AgentAuthorizationContext OperatorAuthorization = new(
        OperatorUserId,
        "operator-user",
        AgentCapabilityScope.All);

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

        var result = await service.ChatAsync(
            new AgentChatRequest("How do I escalate an incident?"),
            ReadOnlyAuthorization,
            CancellationToken.None);

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

        var result = await service.ChatAsync(
            new AgentChatRequest("What orders are delayed?"),
            ReadOnlyAuthorization,
            CancellationToken.None);

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

        var result = await service.ChatAsync(
            new AgentChatRequest("Which suppliers meet the purchasing policy?"),
            ReadOnlyAuthorization,
            CancellationToken.None);

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

        await service.ChatAsync(
            new AgentChatRequest("Create a delay incident."),
            OperatorAuthorization,
            CancellationToken.None);

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
            service.ChatAsync(new AgentChatRequest("Keep looking."), ReadOnlyAuthorization, CancellationToken.None));

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

        var result = await service.ChatAsync(
            new AgentChatRequest("Find the policy."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal("Please provide a valid knowledge search query.", result.Message);
        var toolResult = Assert.IsType<AgentToolMessage>(languageModel.Requests[1].Messages[^1]);
        Assert.True(toolResult.IsError);
        Assert.Contains("valid JSON", toolResult.Content);
    }

    [Fact]
    public async Task ChatAsync_ReadOnlyContext_ExposesAndExecutesLocalSearchAndAllReadMcpTools()
    {
        var session = new FakeToolSession(
            new AgentToolDefinition(AgentToolCapabilityMap.ListSuppliersToolName, "Lists suppliers.", EmptyObjectSchema()),
            new AgentToolDefinition(AgentToolCapabilityMap.GetSupplierToolName, "Gets a supplier.", EmptyObjectSchema()),
            new AgentToolDefinition(AgentToolCapabilityMap.GetDelayedOrdersToolName, "Lists delayed orders.", EmptyObjectSchema()),
            new AgentToolDefinition(AgentToolCapabilityMap.CreateIncidentToolName, "Creates an incident.", EmptyObjectSchema()));
        var languageModel = new ScriptedLanguageModel(
            new AgentLanguageModelResponse(string.Empty,
            [
                new AgentToolCall("call-rag", AgentToolCapabilityMap.SearchKnowledgeToolName, "{\"query\":\"policy\"}"),
                new AgentToolCall("call-list", AgentToolCapabilityMap.ListSuppliersToolName, "{}"),
                new AgentToolCall("call-supplier", AgentToolCapabilityMap.GetSupplierToolName, "{}"),
                new AgentToolCall("call-delayed", AgentToolCapabilityMap.GetDelayedOrdersToolName, "{}")
            ]),
            Final("Read operations completed."));
        var retrieval = new FakeKnowledgeRetrievalService();
        var service = CreateService(languageModel, session, retrieval);

        var result = await service.ChatAsync(
            new AgentChatRequest("Find policy and supplier information."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal(
        [
            AgentToolCapabilityMap.SearchKnowledgeToolName,
            AgentToolCapabilityMap.ListSuppliersToolName,
            AgentToolCapabilityMap.GetSupplierToolName,
            AgentToolCapabilityMap.GetDelayedOrdersToolName
        ], languageModel.Requests[0].Tools.Select(tool => tool.Name));
        Assert.DoesNotContain(
            languageModel.Requests[0].Tools,
            tool => tool.Name == AgentToolCapabilityMap.CreateIncidentToolName);
        Assert.Equal(AgentToolCapabilityMap.SearchKnowledgeToolName, result.ToolsUsed[0]);
        Assert.Equal(
        [
            AgentToolCapabilityMap.ListSuppliersToolName,
            AgentToolCapabilityMap.GetSupplierToolName,
            AgentToolCapabilityMap.GetDelayedOrdersToolName
        ], session.Invocations.Select(invocation => invocation.ToolName));
        Assert.Single(retrieval.Inputs);
    }

    [Fact]
    public async Task ChatAsync_ReadOnlyContext_DeniesManualCreateIncidentWithoutInvokingMcp()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.CreateIncidentToolName,
            "Creates an incident.",
            EmptyObjectSchema()));
        var languageModel = new ScriptedLanguageModel(
            ToolCall(AgentToolCapabilityMap.CreateIncidentToolName, "{}"),
            Final("The incident was not created."));
        var retrieval = new FakeKnowledgeRetrievalService();
        var service = CreateService(languageModel, session, retrieval);

        var result = await service.ChatAsync(
            new AgentChatRequest("Create an incident."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.DoesNotContain(
            languageModel.Requests[0].Tools,
            tool => tool.Name == AgentToolCapabilityMap.CreateIncidentToolName);
        Assert.Empty(session.Invocations);
        Assert.Empty(retrieval.Inputs);
        Assert.Empty(result.ToolsUsed);
        var toolResult = Assert.IsType<AgentToolMessage>(languageModel.Requests[1].Messages[^1]);
        Assert.True(toolResult.IsError);
        Assert.Contains("not authorized", toolResult.Content);
    }

    [Fact]
    public async Task ChatAsync_OperatorContext_ExposesAndExecutesCreateIncident()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.CreateIncidentToolName,
            "Creates an incident.",
            EmptyObjectSchema()));
        var languageModel = new ScriptedLanguageModel(
            ToolCall(AgentToolCapabilityMap.CreateIncidentToolName, "{}"),
            Final("The incident was created."));
        var service = CreateService(languageModel, session, new FakeKnowledgeRetrievalService());

        await service.ChatAsync(
            new AgentChatRequest("Create an incident."),
            OperatorAuthorization,
            CancellationToken.None);

        Assert.Contains(
            languageModel.Requests[0].Tools,
            tool => tool.Name == AgentToolCapabilityMap.CreateIncidentToolName);
        Assert.Equal(AgentToolCapabilityMap.CreateIncidentToolName, session.Invocations.Single().ToolName);
    }

    [Fact]
    public async Task ChatAsync_UnmappedToolIsDeniedByDefaultWithoutInvokingDependencies()
    {
        const string unmappedToolName = "unmapped_tool";
        var session = new FakeToolSession(new AgentToolDefinition(unmappedToolName, "Unmapped.", EmptyObjectSchema()));
        var languageModel = new ScriptedLanguageModel(
            ToolCall(unmappedToolName, "{}"),
            Final("The unmapped tool is unavailable."));
        var retrieval = new FakeKnowledgeRetrievalService();
        var service = CreateService(languageModel, session, retrieval);

        var result = await service.ChatAsync(
            new AgentChatRequest("Use an unmapped tool."),
            OperatorAuthorization,
            CancellationToken.None);

        Assert.DoesNotContain(languageModel.Requests[0].Tools, tool => tool.Name == unmappedToolName);
        Assert.Empty(session.Invocations);
        Assert.Empty(retrieval.Inputs);
        Assert.Empty(result.ToolsUsed);
        Assert.True(Assert.IsType<AgentToolMessage>(languageModel.Requests[1].Messages[^1]).IsError);
    }

    [Fact]
    public async Task ChatAsync_DeniedToolsDoNotInvokeMcpOrRag()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.CreateIncidentToolName,
            "Creates an incident.",
            EmptyObjectSchema()));
        var languageModel = new ScriptedLanguageModel(
            new AgentLanguageModelResponse(string.Empty,
            [
                new AgentToolCall("call-incident", AgentToolCapabilityMap.CreateIncidentToolName, "{}"),
                new AgentToolCall("call-rag", AgentToolCapabilityMap.SearchKnowledgeToolName, "{\"query\":\"policy\"}")
            ]),
            Final("No tools were authorized."));
        var retrieval = new FakeKnowledgeRetrievalService();
        var auditWriter = new FakeAgentToolAuditWriter();
        var service = CreateService(languageModel, session, retrieval, auditWriter: auditWriter);

        var result = await service.ChatAsync(
            new AgentChatRequest("Use every tool."),
            new AgentAuthorizationContext(ReadOnlyUserId, "readonly-user", []),
            CancellationToken.None);

        Assert.Empty(session.Invocations);
        Assert.Empty(retrieval.Inputs);
        Assert.Empty(result.ToolsUsed);
        Assert.Equal(2, auditWriter.Records.Count);
        Assert.All(auditWriter.Records, auditRecord =>
        {
            Assert.False(auditRecord.Authorized);
            Assert.False(auditRecord.Succeeded);
            Assert.Equal("AuthorizationDenied", auditRecord.Outcome);
        });
        Assert.All(
            languageModel.Requests[1].Messages.OfType<AgentToolMessage>(),
            toolMessage => Assert.True(toolMessage.IsError));
    }

    [Fact]
    public async Task ChatAsync_AuditsAuthorizedSuccessWithValidatedIdentityAndWithoutArguments()
    {
        const string secret = "test-only-access-token";
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.ListSuppliersToolName,
            "Lists suppliers.",
            EmptyObjectSchema()));
        var auditWriter = new FakeAgentToolAuditWriter();
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(AgentToolCapabilityMap.ListSuppliersToolName, $"{{\"accessToken\":\"{secret}\"}}"),
                Final("Suppliers listed.")),
            session,
            new FakeKnowledgeRetrievalService(),
            auditWriter: auditWriter);

        await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        var auditRecord = Assert.Single(auditWriter.Records);
        Assert.NotEqual(Guid.Empty, auditRecord.Id);
        Assert.Equal(ReadOnlyUserId, auditRecord.UserId);
        Assert.Equal("readonly-user", auditRecord.Username);
        Assert.Equal(AgentToolCapabilityMap.ListSuppliersToolName, auditRecord.ToolName);
        Assert.Equal(AgentCapabilityScope.SuppliersList.Value, auditRecord.RequiredScope);
        Assert.True(auditRecord.Authorized);
        Assert.True(auditRecord.Succeeded);
        Assert.Equal("Succeeded", auditRecord.Outcome);
        Assert.Equal(1, auditWriter.OutcomeWriteCount);
        Assert.Equal(DateTimeKind.Utc, auditRecord.TimestampUtc.Kind);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(auditRecord), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatAsync_AuditsAuthorizedToolFailuresWithControlledOutcome()
    {
        var session = new FakeToolSession(
            new AgentToolDefinition(
                AgentToolCapabilityMap.ListSuppliersToolName,
                "Lists suppliers.",
                EmptyObjectSchema()),
            new AgentToolExecutionResult("{\"error\":\"upstream failure\"}", IsError: true));
        var auditWriter = new FakeAgentToolAuditWriter();
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(AgentToolCapabilityMap.ListSuppliersToolName, "{}"),
                Final("Supplier lookup failed.")),
            session,
            new FakeKnowledgeRetrievalService(),
            auditWriter: auditWriter);

        await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        var auditRecord = Assert.Single(auditWriter.Records);
        Assert.True(auditRecord.Authorized);
        Assert.False(auditRecord.Succeeded);
        Assert.Equal("ToolExecutionFailed", auditRecord.Outcome);
        Assert.Single(session.Invocations);
    }

    [Fact]
    public async Task ChatAsync_ReturnsSuccessfulToolResultWhenAuditFinalizationFails()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.CreateIncidentToolName,
            "Creates an incident.",
            EmptyObjectSchema()));
        var auditWriter = new FakeAgentToolAuditWriter(throwOnOutcome: true);
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(AgentToolCapabilityMap.CreateIncidentToolName, "{}"),
                Final("The incident was created.")),
            session,
            new FakeKnowledgeRetrievalService(),
            auditWriter: auditWriter);

        var result = await service.ChatAsync(
            new AgentChatRequest("Create an incident."),
            OperatorAuthorization,
            CancellationToken.None);

        Assert.Equal("The incident was created.", result.Message);
        Assert.Equal([AgentToolCapabilityMap.CreateIncidentToolName], result.ToolsUsed);
        Assert.Single(session.Invocations);
        Assert.Equal(1, auditWriter.OutcomeWriteCount);
    }

    [Fact]
    public async Task ChatAsync_PreservesToolFailureWhenAuditFinalizationFails()
    {
        var session = new FakeToolSession(
            new AgentToolDefinition(
                AgentToolCapabilityMap.CreateIncidentToolName,
                "Creates an incident.",
                EmptyObjectSchema()),
            new AgentToolExecutionResult("{\"error\":\"mutation failed\"}", IsError: true));
        var languageModel = new ScriptedLanguageModel(
            ToolCall(AgentToolCapabilityMap.CreateIncidentToolName, "{}"),
            Final("The incident was not created."));
        var auditWriter = new FakeAgentToolAuditWriter(throwOnOutcome: true);
        var service = CreateService(
            languageModel,
            session,
            new FakeKnowledgeRetrievalService(),
            auditWriter: auditWriter);

        var result = await service.ChatAsync(
            new AgentChatRequest("Create an incident."),
            OperatorAuthorization,
            CancellationToken.None);

        Assert.Equal("The incident was not created.", result.Message);
        var toolResult = Assert.IsType<AgentToolMessage>(languageModel.Requests[1].Messages[^1]);
        Assert.True(toolResult.IsError);
        Assert.Equal("{\"error\":\"mutation failed\"}", toolResult.Content);
        Assert.Single(session.Invocations);
        Assert.Equal(1, auditWriter.OutcomeWriteCount);
    }

    [Fact]
    public async Task ChatAsync_DoesNotInvokeToolWhenInitialAuditCreationFails()
    {
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.CreateIncidentToolName,
            "Creates an incident.",
            EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(ToolCall(AgentToolCapabilityMap.CreateIncidentToolName, "{}")),
            session,
            new FakeKnowledgeRetrievalService(),
            auditWriter: new FakeAgentToolAuditWriter(throwOnAttempt: true));

        var exception = await Assert.ThrowsAsync<AgentToolAuditException>(() => service.ChatAsync(
            new AgentChatRequest("Create an incident."),
            OperatorAuthorization,
            CancellationToken.None));

        Assert.Equal("Unable to persist the agent tool audit record.", exception.Message);
        Assert.Empty(session.Invocations);
    }

    private static AgentService CreateService(
        ScriptedLanguageModel languageModel,
        FakeToolSession toolSession,
        FakeKnowledgeRetrievalService retrieval,
        AgentServiceOptions? options = null,
        FakeAgentToolAuditWriter? auditWriter = null)
    {
        return new AgentService(
            languageModel,
            new FakeToolProvider(toolSession),
            retrieval,
            auditWriter ?? new FakeAgentToolAuditWriter(),
            TimeProvider.System,
            NullLogger<AgentService>.Instance,
            options);
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

    private sealed class FakeToolSession : IAgentToolSession
    {
        private readonly AgentToolDefinition[] _tools;
        private readonly AgentToolExecutionResult _result;

        public FakeToolSession(params AgentToolDefinition[] tools)
            : this(tools, result: null)
        {
        }

        public FakeToolSession(AgentToolDefinition tool, AgentToolExecutionResult result)
            : this([tool], result)
        {
        }

        public FakeToolSession(AgentToolDefinition[] tools, AgentToolExecutionResult? result = null)
        {
            _tools = tools;
            _result = result ?? new AgentToolExecutionResult("{\"ok\":true}", IsError: false);
        }

        public List<AgentToolInvocation> Invocations { get; } = [];

        public Task<IReadOnlyList<AgentToolDefinition>> DiscoverToolsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AgentToolDefinition>>(_tools);

        public Task<AgentToolExecutionResult> InvokeAsync(
            AgentToolInvocation invocation,
            CancellationToken cancellationToken)
        {
            Invocations.Add(invocation);
            return Task.FromResult(_result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAgentToolAuditWriter(
        bool throwOnAttempt = false,
        bool throwOnOutcome = false) : IAgentToolAuditWriter
    {
        public List<AgentToolAuditRecord> Records { get; } = [];

        public int OutcomeWriteCount { get; private set; }

        public Task WriteAttemptAsync(AgentToolAuditRecord auditRecord, CancellationToken cancellationToken)
        {
            if (throwOnAttempt)
            {
                throw new InvalidOperationException("Audit attempt write failed.");
            }

            Records.Add(auditRecord);
            return Task.CompletedTask;
        }

        public Task WriteOutcomeAsync(AgentToolAuditRecord auditRecord, CancellationToken cancellationToken)
        {
            OutcomeWriteCount++;

            if (throwOnOutcome)
            {
                throw new InvalidOperationException("Audit outcome write failed.");
            }

            return Task.CompletedTask;
        }
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
