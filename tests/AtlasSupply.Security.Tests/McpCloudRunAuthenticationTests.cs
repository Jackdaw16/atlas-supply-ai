using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AtlasSupply.Infrastructure.Agents;
using Xunit;

namespace AtlasSupply.Security.Tests;

public sealed class McpCloudRunAuthenticationTests
{
    [Fact]
    public async Task LocalhostEndpoint_DoesNotAttachAuthorizationHeader()
    {
        var innerHandler = new RecordingHandler();
        using var client = CreateClient(
            new Uri("http://localhost:5001/mcp"),
            new ThrowingTokenProvider(),
            innerHandler);

        await client.GetAsync("http://localhost:5001/mcp");

        Assert.Null(innerHandler.Authorization);
    }

    [Fact]
    public async Task CloudRunEndpoint_AttachesBearerTokenForServiceAudience()
    {
        var tokenProvider = new RecordingTokenProvider("cloud-run-id-token");
        var innerHandler = new RecordingHandler();
        using var client = CreateClient(
            new Uri("https://atlas-supply-mcp-abc-ew.a.run.app/mcp"),
            tokenProvider,
            innerHandler);

        await client.GetAsync("https://atlas-supply-mcp-abc-ew.a.run.app/mcp");

        Assert.Equal("https://atlas-supply-mcp-abc-ew.a.run.app", tokenProvider.Audience);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "cloud-run-id-token"), innerHandler.Authorization);
    }

    [Fact]
    public void GetAudience_StripsMcpPathQueryAndFragment()
    {
        var audience = McpCloudRunAuthentication.GetAudience(
            new Uri("https://atlas-supply-mcp-abc-ew.a.run.app/mcp?session=test#fragment"));

        Assert.Equal("https://atlas-supply-mcp-abc-ew.a.run.app", audience);
    }

    [Fact]
    public async Task TokenAcquisitionFailure_DoesNotSendAnUnauthenticatedRequest()
    {
        var innerHandler = new RecordingHandler();
        using var client = CreateClient(
            new Uri("https://atlas-supply-mcp-abc-ew.a.run.app/mcp"),
            new ThrowingTokenProvider(),
            innerHandler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("https://atlas-supply-mcp-abc-ew.a.run.app/mcp"));

        Assert.Equal(0, innerHandler.RequestCount);
    }

    [Fact]
    public async Task AuthenticationHandler_PreservesExistingMcpRequestDetails()
    {
        var innerHandler = new RecordingHandler();
        using var client = CreateClient(
            new Uri("http://127.0.0.1:5001/mcp"),
            new ThrowingTokenProvider(),
            innerHandler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5001/mcp")
        {
            Content = new StringContent("{\"method\":\"tools/list\"}", Encoding.UTF8, "application/json")
        };

        await client.SendAsync(request);

        Assert.Equal(HttpMethod.Post, innerHandler.Method);
        Assert.Equal("http://127.0.0.1:5001/mcp", innerHandler.RequestUri);
        Assert.Equal("{\"method\":\"tools/list\"}", innerHandler.Content);
        Assert.Null(innerHandler.Authorization);
    }

    private static HttpClient CreateClient(
        Uri endpoint,
        IMcpIdTokenProvider tokenProvider,
        HttpMessageHandler innerHandler)
    {
        return new HttpClient(new McpCloudRunAuthenticationHandler(endpoint, tokenProvider)
        {
            InnerHandler = innerHandler
        });
    }

    private sealed class RecordingTokenProvider(string token) : IMcpIdTokenProvider
    {
        internal string? Audience { get; private set; }

        public Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken)
        {
            Audience = audience;
            return Task.FromResult(token);
        }
    }

    private sealed class ThrowingTokenProvider : IMcpIdTokenProvider
    {
        public Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Unable to obtain an MCP ID token.");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal AuthenticationHeaderValue? Authorization { get; private set; }
        internal string? Content { get; private set; }
        internal HttpMethod? Method { get; private set; }
        internal string? RequestUri { get; private set; }
        internal int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Authorization = request.Headers.Authorization;
            Content = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Method = request.Method;
            RequestUri = request.RequestUri!.ToString();

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
