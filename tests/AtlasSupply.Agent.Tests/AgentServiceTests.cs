using System.Text.Json;
using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.Extensions.Logging;
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
    public async Task ChatAsync_DisabledShadowRoutingDoesNotInvokeRouterOrChangePrimaryBehavior()
    {
        var router = new FakeAgentRouter(CreateRoutingDecision(AgentToolCapabilityMap.ListSuppliersToolName));
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.ListSuppliersToolName,
            "Lists suppliers.",
            EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(AgentToolCapabilityMap.ListSuppliersToolName, "{}"),
                Final("Suppliers listed.")),
            session,
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: false),
            router: router);

        var result = await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Empty(router.Requests);
        Assert.Equal("Suppliers listed.", result.Message);
        Assert.Equal([AgentToolCapabilityMap.ListSuppliersToolName], result.ToolsUsed);
        Assert.Single(session.Invocations);
    }

    [Fact]
    public async Task ChatAsync_EnabledShadowRoutingUsesAuthorizedToolCatalogOnly()
    {
        var router = new FakeAgentRouter(CreateRoutingDecision(AgentRoute.General));
        var session = new FakeToolSession(
            new AgentToolDefinition(AgentToolCapabilityMap.ListSuppliersToolName, "Lists suppliers.", EmptyObjectSchema()),
            new AgentToolDefinition(AgentToolCapabilityMap.CreateIncidentToolName, "Creates an incident.", EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(Final("Suppliers are available.")),
            session,
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: router);

        await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        var routingRequest = Assert.Single(router.Requests);
        Assert.Equal("List suppliers.", routingRequest.Message);
        Assert.Equal(
            [AgentToolCapabilityMap.SearchKnowledgeToolName, AgentToolCapabilityMap.ListSuppliersToolName],
            routingRequest.AvailableTools);
        Assert.DoesNotContain(AgentToolCapabilityMap.CreateIncidentToolName, routingRequest.AvailableTools);
    }

    [Theory]
    [InlineData(AgentToolCapabilityMap.ListSuppliersToolName, true)]
    [InlineData(AgentRoute.General, false)]
    public async Task ChatAsync_EnabledShadowRoutingComparesSingleFirstToolCall(string jevRoute, bool expectedMatch)
    {
        var router = new FakeAgentRouter(CreateRoutingDecision(jevRoute));
        var logger = new ListLogger<AgentService>();
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.ListSuppliersToolName,
            "Lists suppliers.",
            EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(AgentToolCapabilityMap.ListSuppliersToolName, "{}"),
                Final("Suppliers listed.")),
            session,
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: router,
            logger: logger);

        await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Single(router.Requests);
        var comparison = Assert.Single(
            logger.Entries,
            entry => entry.EventId.Name == "AgentRoutingShadowComparison");
        Assert.Equal(LogLevel.Information, comparison.LogLevel);
        Assert.Equal(1, comparison.EventId.Id);
        Assert.Equal(
            "Jev shadow comparison: Jev={JevRoute} ({JevProbability:P1}) OpenAI={LlmRoute} Tools={LlmToolNames} Comparable={Comparable} Matched={Matched} LatencyMs={JevElapsedMilliseconds} CostUsd={JevEstimatedCostUsd} JevProbabilities={JevProbabilities} LlmToolCount={LlmToolCount} JevInputTokens={JevInputTokens} JevOutputTokens={JevOutputTokens}",
            comparison.Properties["{OriginalFormat}"]);
        Assert.Equal(jevRoute, comparison.Properties["JevRoute"]);
        Assert.Equal(0.91m, comparison.Properties["JevProbability"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, decimal>>(comparison.Properties["JevProbabilities"]);
        Assert.Equal(AgentToolCapabilityMap.ListSuppliersToolName, comparison.Properties["LlmRoute"]);
        Assert.Equal([AgentToolCapabilityMap.ListSuppliersToolName], Assert.IsAssignableFrom<IEnumerable<string>>(comparison.Properties["LlmToolNames"]));
        Assert.Equal(1, comparison.Properties["LlmToolCount"]);
        Assert.Equal(true, comparison.Properties["Comparable"]);
        Assert.Equal(expectedMatch, comparison.Properties["Matched"]);
        Assert.Equal(17L, comparison.Properties["JevElapsedMilliseconds"]);
        Assert.Equal(12, comparison.Properties["JevInputTokens"]);
        Assert.Equal(8, comparison.Properties["JevOutputTokens"]);
        Assert.Equal(0.000013m, comparison.Properties["JevEstimatedCostUsd"]);
    }

    [Fact]
    public async Task ChatAsync_EnabledShadowRoutingMapsNoFirstToolCallsToGeneral()
    {
        var router = new FakeAgentRouter(CreateRoutingDecision(AgentRoute.General));
        var logger = new ListLogger<AgentService>();
        var service = CreateService(
            new ScriptedLanguageModel(Final("General response.")),
            new FakeToolSession(),
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: router,
            logger: logger);

        var result = await service.ChatAsync(
            new AgentChatRequest("Hello."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal("General response.", result.Message);
        var comparison = Assert.Single(
            logger.Entries,
            entry => entry.EventId.Name == "AgentRoutingShadowComparison");
        Assert.Equal(AgentRoute.General, comparison.Properties["LlmRoute"]);
        Assert.Equal(0, comparison.Properties["LlmToolCount"]);
        Assert.Equal(true, comparison.Properties["Comparable"]);
        Assert.Equal(true, comparison.Properties["Matched"]);
    }

    [Fact]
    public async Task ChatAsync_EnabledShadowRoutingMarksMultipleFirstToolCallsNonComparable()
    {
        var router = new FakeAgentRouter(CreateRoutingDecision(AgentToolCapabilityMap.ListSuppliersToolName));
        var logger = new ListLogger<AgentService>();
        var session = new FakeToolSession(
            new AgentToolDefinition(AgentToolCapabilityMap.ListSuppliersToolName, "Lists suppliers.", EmptyObjectSchema()),
            new AgentToolDefinition(AgentToolCapabilityMap.GetDelayedOrdersToolName, "Lists delayed orders.", EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(
                new AgentLanguageModelResponse(string.Empty,
                [
                    new AgentToolCall("call-list", AgentToolCapabilityMap.ListSuppliersToolName, "{}"),
                    new AgentToolCall("call-delayed", AgentToolCapabilityMap.GetDelayedOrdersToolName, "{}")
                ]),
                Final("Supplier and order details listed.")),
            session,
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: router,
            logger: logger);

        var result = await service.ChatAsync(
            new AgentChatRequest("List suppliers and delayed orders."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal(
            [AgentToolCapabilityMap.ListSuppliersToolName, AgentToolCapabilityMap.GetDelayedOrdersToolName],
            result.ToolsUsed);
        Assert.Single(router.Requests);
        var comparison = Assert.Single(
            logger.Entries,
            entry => entry.EventId.Name == "AgentRoutingShadowComparison");
        Assert.Null(comparison.Properties["LlmRoute"]);
        Assert.Equal(
            [AgentToolCapabilityMap.ListSuppliersToolName, AgentToolCapabilityMap.GetDelayedOrdersToolName],
            Assert.IsAssignableFrom<IEnumerable<string>>(comparison.Properties["LlmToolNames"]));
        Assert.Equal(2, comparison.Properties["LlmToolCount"]);
        Assert.Equal(false, comparison.Properties["Comparable"]);
        Assert.Null(comparison.Properties["Matched"]);
    }

    [Fact]
    public async Task ChatAsync_ShadowProviderFailureLogsSafeStructuredExceptionDetailsAndPreservesNormalFlow()
    {
        const string userMessage = "List suppliers for user 123 with private contract details.";
        const string bearerToken = "Bearer bearer-value-must-not-be-logged";
        const string apiKey = "api-key-value-must-not-be-logged";
        const string token = "token-value-must-not-be-logged";
        var router = new FakeAgentRouter(exception: new AgentRoutingProviderException(
            statusCode: 400,
            reasonPhrase: "Bad\r\nRequest",
            responseBody: $$"""
                {
                  "error": {
                    "message": "Rejected request: {{userMessage}}",
                    "code": "invalid_request",
                    "type": "invalid_request_error",
                    "authorization": "{{bearerToken}}",
                    "apiKey": "{{apiKey}}",
                    "details": { "token": "{{token}}" }
                  }
                }
                """,
            new HttpRequestException("Agent routing provider transport request failed.")));
        var logger = new ListLogger<AgentService>();
        var session = new FakeToolSession(new AgentToolDefinition(
            AgentToolCapabilityMap.ListSuppliersToolName,
            "Lists suppliers.",
            EmptyObjectSchema()));
        var service = CreateService(
            new ScriptedLanguageModel(
                ToolCall(AgentToolCapabilityMap.ListSuppliersToolName, "{}"),
                Final("Suppliers listed.")),
            session,
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: router,
            logger: logger);

        var result = await service.ChatAsync(
            new AgentChatRequest(userMessage),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal("Suppliers listed.", result.Message);
        Assert.Equal([AgentToolCapabilityMap.ListSuppliersToolName], result.ToolsUsed);
        Assert.Single(session.Invocations);
        var failure = Assert.Single(
            logger.Entries,
            entry => entry.EventId.Name == "AgentRoutingShadowFailure");
        Assert.Equal(LogLevel.Warning, failure.LogLevel);
        Assert.Equal("ProviderFailure", failure.Properties["ShadowFailureType"]);
        Assert.Equal(nameof(AgentRoutingProviderException), failure.Properties["ExceptionType"]);
        Assert.Equal(
            "Agent routing provider returned status 400 (Bad Request).",
            failure.Properties["ExceptionMessage"]);
        Assert.Equal(nameof(HttpRequestException), failure.Properties["InnerExceptionType"]);
        Assert.Equal(
            "Agent routing provider transport request failed.",
            failure.Properties["InnerExceptionMessage"]);
        Assert.Equal("HttpResponse", failure.Properties["ProviderFailureKind"]);
        Assert.Equal(400, failure.Properties["ProviderStatusCode"]);
        Assert.Equal("Bad Request", failure.Properties["ProviderReasonPhrase"]);
        var excerpt = Assert.IsType<string>(failure.Properties["ProviderResponseExcerpt"]);
        using var excerptDocument = JsonDocument.Parse(excerpt);
        Assert.Equal("invalid_request", excerptDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal("invalid_request_error", excerptDocument.RootElement.GetProperty("type").GetString());
        Assert.False(excerptDocument.RootElement.TryGetProperty("message", out _));
        Assert.DoesNotContain(userMessage, excerpt, StringComparison.Ordinal);
        Assert.DoesNotContain(bearerToken, excerpt, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, excerpt, StringComparison.Ordinal);
        Assert.DoesNotContain(token, excerpt, StringComparison.Ordinal);
        Assert.Null(failure.Exception);
    }

    [Fact]
    public async Task ChatAsync_ShadowTransportProviderFailureLogsTransportDetails()
    {
        var logger = new ListLogger<AgentService>();
        var service = CreateService(
            new ScriptedLanguageModel(Final("Suppliers listed.")),
            new FakeToolSession(),
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: new FakeAgentRouter(exception: new AgentRoutingProviderException(
                statusCode: null,
                reasonPhrase: null,
                responseBody: null,
                new HttpRequestException("Agent routing provider transport request failed."))),
            logger: logger);

        var result = await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal("Suppliers listed.", result.Message);
        var failure = Assert.Single(
            logger.Entries,
            entry => entry.EventId.Name == "AgentRoutingShadowFailure");
        Assert.Equal("Transport", failure.Properties["ProviderFailureKind"]);
        Assert.Null(failure.Properties["ProviderStatusCode"]);
        Assert.Null(failure.Properties["ProviderReasonPhrase"]);
        Assert.Null(failure.Properties["ProviderResponseExcerpt"]);
        Assert.Null(failure.Exception);
    }

    [Theory]
    [MemberData(nameof(ShadowRoutingFailures))]
    public async Task ChatAsync_ShadowRoutingClassifiesResponseValidationRequestValidationAndUnknownFailures(
        Exception exception,
        string expectedFailureType)
    {
        var logger = new ListLogger<AgentService>();
        var service = CreateService(
            new ScriptedLanguageModel(Final("Suppliers listed.")),
            new FakeToolSession(),
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: new FakeAgentRouter(exception: exception),
            logger: logger);

        var result = await service.ChatAsync(
            new AgentChatRequest("List suppliers."),
            ReadOnlyAuthorization,
            CancellationToken.None);

        Assert.Equal("Suppliers listed.", result.Message);
        var failure = Assert.Single(
            logger.Entries,
            entry => entry.EventId.Name == "AgentRoutingShadowFailure");
        Assert.Equal(expectedFailureType, failure.Properties["ShadowFailureType"]);
        Assert.Equal(exception.GetType().Name, failure.Properties["ExceptionType"]);
        Assert.Equal(exception.Message, failure.Properties["ExceptionMessage"]);
        Assert.Null(failure.Properties["InnerExceptionType"]);
        Assert.Null(failure.Properties["InnerExceptionMessage"]);
        Assert.Null(failure.Properties["ProviderFailureKind"]);
        Assert.Null(failure.Properties["ProviderStatusCode"]);
        Assert.Null(failure.Properties["ProviderReasonPhrase"]);
        Assert.Null(failure.Properties["ProviderResponseExcerpt"]);
        Assert.Null(failure.Exception);
    }

    public static IEnumerable<object[]> ShadowRoutingFailures()
    {
        yield return [new AgentRoutingResponseException("answers.route", "must be a JSON object"), "ResponseValidationFailure"];
        yield return [new ArgumentException("Agent routing message is required.", "request"), "RequestValidationFailure"];
        yield return [new InvalidOperationException("Unexpected shadow failure."), "UnknownFailure"];
    }

    [Fact]
    public async Task ChatAsync_EnabledShadowRoutingSkipsRouterWithoutAuthorizedTools()
    {
        var router = new FakeAgentRouter(CreateRoutingDecision(AgentRoute.General));
        var service = CreateService(
            new ScriptedLanguageModel(Final("No tools are available.")),
            new FakeToolSession(new AgentToolDefinition(
                AgentToolCapabilityMap.CreateIncidentToolName,
                "Creates an incident.",
                EmptyObjectSchema())),
            new FakeKnowledgeRetrievalService(),
            options: new AgentServiceOptions(ExperimentalJevShadowRouting: true),
            router: router);

        var result = await service.ChatAsync(
            new AgentChatRequest("Create an incident."),
            new AgentAuthorizationContext(ReadOnlyUserId, "readonly-user", []),
            CancellationToken.None);

        Assert.Equal("No tools are available.", result.Message);
        Assert.Empty(router.Requests);
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
        Assert.Contains(
            "cannot perform write operations such as creating incidents",
            languageModel.Requests[0].SystemInstructions,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "permissions available in this session",
            languageModel.Requests[0].SystemInstructions,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("incidents.create", languageModel.Requests[0].SystemInstructions, StringComparison.Ordinal);
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
        Assert.Contains("You can create incidents.", languageModel.Requests[0].SystemInstructions, StringComparison.Ordinal);
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
        FakeAgentToolAuditWriter? auditWriter = null,
        FakeAgentRouter? router = null,
        ILogger<AgentService>? logger = null)
    {
        return new AgentService(
            languageModel,
            new FakeToolProvider(toolSession),
            retrieval,
            auditWriter ?? new FakeAgentToolAuditWriter(),
            TimeProvider.System,
            logger ?? NullLogger<AgentService>.Instance,
            options,
            router);
    }

    private static AgentRoutingDecision CreateRoutingDecision(string selectedRoute)
    {
        var probabilities = new Dictionary<string, decimal>
        {
            [selectedRoute] = 0.91m
        };
        if (selectedRoute != AgentRoute.General)
        {
            probabilities.Add(AgentRoute.General, 0.09m);
        }

        return new AgentRoutingDecision(
            selectedRoute,
            0.91m,
            probabilities,
            new AgentRoutingTelemetry(
                Provider: "test-provider",
                Model: "test-model",
                InputTokens: 12,
                OutputTokens: 8,
                EstimatedCostUsd: 0.000013m,
                ElapsedMilliseconds: 17));
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

    private sealed class FakeAgentRouter(
        AgentRoutingDecision? decision = null,
        Exception? exception = null) : IAgentRouter
    {
        public List<AgentRoutingRequest> Requests { get; } = [];

        public Task<AgentRoutingDecision> RouteAsync(
            AgentRoutingRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return exception is null
                ? Task.FromResult(decision ?? CreateRoutingDecision(AgentRoute.General))
                : Task.FromException<AgentRoutingDecision>(exception);
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, eventId, properties, exception));
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception);

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
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
