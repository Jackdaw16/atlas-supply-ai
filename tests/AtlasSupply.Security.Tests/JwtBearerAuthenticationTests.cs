using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AtlasSupply.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AtlasSupply.Security.Tests;

public sealed class JwtBearerAuthenticationTests
{
    private const string Issuer = "atlas-supply-api-tests";
    private const string Audience = "atlas-supply-api-tests";
    private const string SigningKey = "test-signing-key-with-at-least-thirty-two-bytes";

    [Fact]
    public async Task Chat_WithoutToken_ReturnsUnauthorizedWithoutInvokingAgentDependencies()
    {
        using var factory = new AuthenticatedApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat", new AgentChatRequest("Hello"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.LanguageModel.CompletionCount);
        Assert.Equal(0, factory.ToolProvider.OpenSessionCount);
    }

    [Fact]
    public async Task Chat_WithValidToken_ProceedsWithoutUsingOpenAiOrMcpImplementations()
    {
        using var factory = new AuthenticatedApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.PostAsJsonAsync("/api/chat", new AgentChatRequest("Hello"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.LanguageModel.CompletionCount);
        Assert.Equal(1, factory.ToolProvider.OpenSessionCount);
    }

    [Fact]
    public async Task Chat_DerivesToolCapabilitiesFromTheValidatedJwtScopeClaimOnly()
    {
        using var factory = new AuthenticatedApiFactory();
        factory.ToolProvider.Tools =
        [
            new AgentToolDefinition("get_supplier", "Gets a supplier.", EmptyObjectSchema()),
            new AgentToolDefinition("create_incident", "Creates an incident.", EmptyObjectSchema())
        ];
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { message = "Hello", scopes = new[] { "incidents.create" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["get_supplier"], factory.LanguageModel.Requests.Single().Tools.Select(tool => tool.Name));
    }

    [Fact]
    public async Task Chat_WithInvalidSignature_ReturnsUnauthorized()
    {
        await AssertRejectedChatTokenAsync(CreateToken(signingKey: "different-test-signing-key-with-at-least-32-bytes"));
    }

    [Fact]
    public async Task Chat_WithExpiredToken_ReturnsUnauthorized()
    {
        await AssertRejectedChatTokenAsync(CreateToken(expiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task Chat_WithWrongIssuer_ReturnsUnauthorized()
    {
        await AssertRejectedChatTokenAsync(CreateToken(issuer: "unexpected-issuer"));
    }

    [Fact]
    public async Task Chat_WithWrongAudience_ReturnsUnauthorized()
    {
        await AssertRejectedChatTokenAsync(CreateToken(audience: "unexpected-audience"));
    }

    [Fact]
    public async Task Login_RemainsAnonymous()
    {
        using var factory = new AuthenticatedApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task AssertRejectedChatTokenAsync(string token)
    {
        using var factory = new AuthenticatedApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/chat", new AgentChatRequest("Hello"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.LanguageModel.CompletionCount);
        Assert.Equal(0, factory.ToolProvider.OpenSessionCount);
    }

    private static string CreateToken(
        string? issuer = null,
        string? audience = null,
        string? signingKey = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = expiresAtUtc ?? now.AddMinutes(5);
        var notBefore = now.AddMinutes(-1);
        if (expires <= notBefore)
        {
            notBefore = expires.AddMinutes(-1);
        }

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")),
            new Claim("name", "test-user"),
            new Claim("preferred_username", "test-user"),
            new Claim("scope", "suppliers.read")
        };
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: claims,
            notBefore: notBefore.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static JsonElement EmptyObjectSchema()
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\"}");
        return document.RootElement.Clone();
    }

    private sealed class AuthenticatedApiFactory : WebApplicationFactory<Program>
    {
        internal TestAgentLanguageModel LanguageModel { get; } = new();
        internal TestAgentToolProvider ToolProvider { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["JWT_SIGNING_KEY"] = SigningKey,
                    ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=atlas_supply_tests"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAgentLanguageModel>();
                services.RemoveAll<IAgentToolProvider>();
                services.RemoveAll<IKnowledgeRetrievalService>();
                services.AddSingleton<IAgentLanguageModel>(LanguageModel);
                services.AddSingleton<IAgentToolProvider>(ToolProvider);
                services.AddSingleton<IKnowledgeRetrievalService>(new TestKnowledgeRetrievalService());
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                        ValidateIssuer = true,
                        ValidIssuer = Issuer,
                        ValidateAudience = true,
                        ValidAudience = Audience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.Zero,
                        NameClaimType = "name"
                    };
                });
            });
        }
    }

    private sealed class TestAgentLanguageModel : IAgentLanguageModel
    {
        internal int CompletionCount { get; private set; }

        internal List<AgentLanguageModelRequest> Requests { get; } = [];

        public Task<AgentLanguageModelResponse> CompleteAsync(
            AgentLanguageModelRequest request,
            CancellationToken cancellationToken)
        {
            CompletionCount++;
            Requests.Add(request);
            return Task.FromResult(new AgentLanguageModelResponse("Test response", []));
        }
    }

    private sealed class TestAgentToolProvider : IAgentToolProvider
    {
        internal int OpenSessionCount { get; private set; }

        internal IReadOnlyList<AgentToolDefinition> Tools { get; set; } = [];

        public Task<IAgentToolSession> OpenSessionAsync(CancellationToken cancellationToken)
        {
            OpenSessionCount++;
            return Task.FromResult<IAgentToolSession>(new TestAgentToolSession(Tools));
        }
    }

    private sealed class TestAgentToolSession(IReadOnlyList<AgentToolDefinition> tools) : IAgentToolSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<IReadOnlyList<AgentToolDefinition>> DiscoverToolsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(tools);
        }

        public Task<AgentToolExecutionResult> InvokeAsync(
            AgentToolInvocation invocation,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The test language model does not request tools.");
        }
    }

    private sealed class TestKnowledgeRetrievalService : IKnowledgeRetrievalService
    {
        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            KnowledgeSearchInput input,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeSearchResult>>([]);
        }
    }
}
