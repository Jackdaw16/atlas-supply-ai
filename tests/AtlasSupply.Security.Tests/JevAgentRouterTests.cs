using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AtlasSupply.Application;
using AtlasSupply.Infrastructure.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtlasSupply.Security.Tests;

public sealed class JevAgentRouterTests
{
    [Fact]
    public async Task RouteAsync_SerializesChoiceCriteriaForSubmittedToolAndGeneral()
    {
        var handler = new RecordingHandler(SuccessResponse());
        using var fixture = CreateFixture(handler);

        await fixture.Router.RouteAsync(
            new AgentRoutingRequest("Which orders are delayed?", ["get_delayed_orders"]),
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.Content!);
        var root = document.RootElement;
        Assert.Equal("typesafe-ai/jev", root.GetProperty("model").GetString());
        Assert.Equal("Which orders are delayed?", root.GetProperty("input").GetString());
        Assert.True(root.GetProperty("providerOptions").GetProperty("gateway").GetProperty("zeroDataRetention").GetBoolean());
        Assert.Equal(["typesafe-ai"], root.GetProperty("providerOptions").GetProperty("gateway").GetProperty("only").EnumerateArray().Select(value => value.GetString()));

        var question = Assert.Single(root.GetProperty("questions").EnumerateArray());
        Assert.Equal("route", question.GetProperty("name").GetString());
        Assert.Equal("choice", question.GetProperty("type").GetString());
        Assert.Equal(
            ["get_delayed_orders", AgentRoute.General],
            question.GetProperty("criteria").EnumerateArray().Select(value => value.GetProperty("name").GetString()));
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-gateway-key"), handler.Authorization);
    }

    [Fact]
    public async Task RouteAsync_ReturnsChoiceDistributionAndUsageTelemetry()
    {
        var handler = new RecordingHandler("""
            {
              "answers": {
                "route": {
                  "choice": "get_delayed_orders",
                  "probabilities": {
                    "get_delayed_orders": 0.97,
                    "general": 0.03
                  }
                }
              },
              "model": "typesafe-ai/jev",
              "usage": { "inputTokens": 12, "outputTokens": 8 },
              "providerMetadata": {
                "gateway": { "provider": "typesafe-ai", "cost": "0.000013" }
              }
            }
            """);
        using var fixture = CreateFixture(handler);

        var decision = await fixture.Router.RouteAsync(
            new AgentRoutingRequest("Which orders are delayed?", ["get_delayed_orders"]),
            CancellationToken.None);

        Assert.Equal("get_delayed_orders", decision.SelectedRoute);
        Assert.Equal(0.97m, decision.SelectedProbability);
        Assert.Equal(
            new Dictionary<string, decimal>
            {
                ["get_delayed_orders"] = 0.97m,
                [AgentRoute.General] = 0.03m
            },
            decision.ProbabilityDistribution);
        Assert.Equal("typesafe-ai", decision.Telemetry.Provider);
        Assert.Equal("typesafe-ai/jev", decision.Telemetry.Model);
        Assert.Equal(12, decision.Telemetry.InputTokens);
        Assert.Equal(8, decision.Telemetry.OutputTokens);
        Assert.Equal(0.000013m, decision.Telemetry.EstimatedCostUsd);
        Assert.True(decision.Telemetry.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public async Task RouteAsync_MissingApiKeyFailsOnlyWhenInvoked()
    {
        var handler = new RecordingHandler(SuccessResponse());
        using var fixture = CreateFixture(handler, apiKey: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("Which orders are delayed?", ["get_delayed_orders"]),
            CancellationToken.None));

        Assert.Equal(
            "AI Gateway API key is missing. Configure AI_GATEWAY_API_KEY before invoking agent routing.",
            exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [MemberData(nameof(MalformedResponses))]
    public async Task RouteAsync_RejectsMalformedOrInvalidGatewayResponses(string response)
    {
        using var fixture = CreateFixture(new RecordingHandler(response));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("Which orders are delayed?", ["get_delayed_orders"]),
            CancellationToken.None));

        Assert.Contains("malformed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RouteAsync_RejectsNonSuccessGatewayResponse()
    {
        using var fixture = CreateFixture(new RecordingHandler("{}", HttpStatusCode.BadGateway));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("Which orders are delayed?", ["get_delayed_orders"]),
            CancellationToken.None));

        Assert.Equal("AI Gateway evaluation failed with HTTP status 502.", exception.Message);
    }

    [Fact]
    public async Task RouteAsync_RejectsInvalidRoutingRequestsWithoutSendingHttp()
    {
        var handler = new RecordingHandler(SuccessResponse());
        using var fixture = CreateFixture(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Router.RouteAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest(" ", ["get_delayed_orders"]), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("message", []), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("message", [" "]), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("message", ["get_delayed_orders", "get_delayed_orders"]), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Router.RouteAsync(
            new AgentRoutingRequest("message", [AgentRoute.General]), CancellationToken.None));

        Assert.Equal(0, handler.RequestCount);
    }

    public static IEnumerable<object[]> MalformedResponses()
    {
        yield return ["{not-json"];
        yield return ["{\"answers\":{}}"];
        yield return ["{\"answers\":{\"route\":{\"choice\":\"unknown_tool\",\"probabilities\":{\"unknown_tool\":0.97}}}}"];
        yield return ["{\"answers\":{\"route\":{\"choice\":\"get_delayed_orders\",\"probabilities\":{\"general\":0.03}}}}"];
        yield return ["{\"answers\":{\"route\":{\"choice\":\"get_delayed_orders\",\"probabilities\":{\"get_delayed_orders\":1.01}}}}"];
        yield return ["{\"answers\":{\"route\":{\"choice\":\"get_delayed_orders\",\"probabilities\":{\"get_delayed_orders\":0.97}}},\"providerMetadata\":{\"gateway\":{\"cost\":\"not-a-decimal\"}}}"];
    }

    private static RouterFixture CreateFixture(RecordingHandler handler, string? apiKey = "test-gateway-key")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI_GATEWAY_API_KEY"] = apiKey
            })
            .Build();
        var services = new ServiceCollection();
        services.AddHttpClient(JevAgentRouter.HttpClientName, client =>
                client.BaseAddress = new Uri("https://ai-gateway.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var serviceProvider = services.BuildServiceProvider();
        return new RouterFixture(
            serviceProvider,
            new JevAgentRouter(
                configuration,
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                NullLogger<JevAgentRouter>.Instance));
    }

    private static string SuccessResponse() => """
        {
          "answers": {
            "route": {
              "choice": "get_delayed_orders",
              "probabilities": {
                "get_delayed_orders": 0.97,
                "general": 0.03
              }
            }
          }
        }
        """;

    private sealed class RouterFixture(ServiceProvider serviceProvider, JevAgentRouter router) : IDisposable
    {
        public JevAgentRouter Router { get; } = router;

        public void Dispose() => serviceProvider.Dispose();
    }

    private sealed class RecordingHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal AuthenticationHeaderValue? Authorization { get; private set; }

        internal string? Content { get; private set; }

        internal int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Authorization = request.Headers.Authorization;
            Content = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
        }
    }
}
