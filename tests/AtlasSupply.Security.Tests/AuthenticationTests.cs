using System.IdentityModel.Tokens.Jwt;
using AtlasSupply.Application;
using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AtlasSupply.Security.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task Login_ReadOnlyUser_ReturnsAssignedScopes()
    {
        var fixture = new AuthenticationFixture();

        var result = await fixture.Login.ExecuteAsync(new LoginInput("readonly", AuthenticationFixture.Password), CancellationToken.None);

        Assert.Equal("readonly", result.Username);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal(
            ["knowledge.search", "orders.delayed.read", "suppliers.list", "suppliers.read"],
            result.Scopes);
    }

    [Fact]
    public async Task Login_OperatorUser_ReturnsIncidentCreationScope()
    {
        var fixture = new AuthenticationFixture();

        var result = await fixture.Login.ExecuteAsync(new LoginInput("operator", AuthenticationFixture.Password), CancellationToken.None);

        Assert.Equal("operator", result.Username);
        Assert.Contains("incidents.create", result.Scopes);
    }

    [Fact]
    public async Task Login_InvalidPassword_ThrowsGenericFailure()
    {
        var fixture = new AuthenticationFixture();

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            fixture.Login.ExecuteAsync(new LoginInput("readonly", "invalid-password"), CancellationToken.None));

        Assert.Equal("Invalid username or password.", exception.Message);
    }

    [Fact]
    public async Task Login_UnknownUser_ThrowsTheSameGenericFailureAsInvalidPassword()
    {
        var fixture = new AuthenticationFixture();

        var invalidPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            fixture.Login.ExecuteAsync(new LoginInput("readonly", "invalid-password"), CancellationToken.None));
        var unknownUser = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            fixture.Login.ExecuteAsync(new LoginInput("unknown", AuthenticationFixture.Password), CancellationToken.None));

        Assert.Equal(invalidPassword.Message, unknownUser.Message);
    }

    [Fact]
    public async Task JwtAccessToken_ReadOnlyUserContainsExactlyFourScopes()
    {
        var fixture = new AuthenticationFixture(new JwtAccessTokenIssuer(CreateJwtConfiguration(), AuthenticationFixture.TimeProvider));

        var result = await fixture.Login.ExecuteAsync(new LoginInput("readonly", AuthenticationFixture.Password), CancellationToken.None);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        var scope = Assert.Single(token.Claims, claim => claim.Type == "scope");
        Assert.Equal("knowledge.search orders.delayed.read suppliers.list suppliers.read", scope.Value);
        Assert.Equal(4, scope.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task JwtAccessToken_OperatorUserContainsIncidentCreationScope()
    {
        var fixture = new AuthenticationFixture(new JwtAccessTokenIssuer(CreateJwtConfiguration(), AuthenticationFixture.TimeProvider));

        var result = await fixture.Login.ExecuteAsync(new LoginInput("operator", AuthenticationFixture.Password), CancellationToken.None);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Contains("incidents.create", Assert.Single(token.Claims, claim => claim.Type == "scope").Value.Split(' '));
    }

    [Fact]
    public async Task JwtAccessToken_UsesConfiguredExpirationIssuerAndAudience()
    {
        var fixture = new AuthenticationFixture(new JwtAccessTokenIssuer(CreateJwtConfiguration(), AuthenticationFixture.TimeProvider));

        var result = await fixture.Login.ExecuteAsync(new LoginInput("readonly", AuthenticationFixture.Password), CancellationToken.None);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal("atlas-supply-tests", token.Issuer);
        Assert.Equal(["atlas-supply-api-tests"], token.Audiences);
        Assert.Equal(AuthenticationFixture.TimeProvider.GetUtcNow().AddMinutes(15), result.ExpiresAtUtc);
        Assert.Equal(result.ExpiresAtUtc.UtcDateTime, token.ValidTo);
        Assert.Equal(AuthenticationFixture.TimeProvider.GetUtcNow().UtcDateTime, token.Payload.IssuedAt);
        Assert.Equal(result.ExpiresAtUtc.ToUnixTimeSeconds(), token.Payload.Expiration.GetValueOrDefault());
    }

    private static IConfiguration CreateJwtConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "atlas-supply-tests",
                ["Jwt:Audience"] = "atlas-supply-api-tests",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["JWT_SIGNING_KEY"] = "test-signing-key-with-at-least-thirty-two-bytes"
            })
            .Build();
    }

    private sealed class AuthenticationFixture
    {
        internal const string Password = "test-only-password";
        internal static readonly TimeProvider TimeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero));

        internal AuthenticationFixture(IAccessTokenIssuer? accessTokenIssuer = null)
        {
            var passwordHasher = new UserPasswordHasher();
            var readOnlyUser = CreateUser(
                passwordHasher,
                "readonly",
                AgentCapabilityScope.SuppliersList,
                AgentCapabilityScope.SuppliersRead,
                AgentCapabilityScope.OrdersDelayedRead,
                AgentCapabilityScope.KnowledgeSearch);
            var operatorUser = CreateUser(
                passwordHasher,
                "operator",
                AgentCapabilityScope.SuppliersList,
                AgentCapabilityScope.SuppliersRead,
                AgentCapabilityScope.OrdersDelayedRead,
                AgentCapabilityScope.IncidentsCreate,
                AgentCapabilityScope.KnowledgeSearch);

            Login = new Login(
                new InMemoryUserCredentialStore([readOnlyUser, operatorUser]),
                passwordHasher,
                accessTokenIssuer ?? new TestAccessTokenIssuer());
        }

        internal Login Login { get; }

        private static User CreateUser(
            UserPasswordHasher passwordHasher,
            string username,
            params AgentCapabilityScope[] scopes)
        {
            var user = new User(Guid.NewGuid(), username, $"{username}@atlas-supply.test", "placeholder");
            user.SetPasswordHash(passwordHasher.HashPassword(user, Password));

            foreach (var scope in scopes)
            {
                user.AssignScope(scope);
            }

            return user;
        }
    }

    private sealed class InMemoryUserCredentialStore(IReadOnlyList<User> users) : IUserCredentialStore
    {
        public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            return Task.FromResult(users.SingleOrDefault(user =>
                string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private sealed class TestAccessTokenIssuer : IAccessTokenIssuer
    {
        public Task<IssuedAccessToken> IssueAsync(AccessTokenIssueRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IssuedAccessToken(
                "access-token",
                AuthenticationFixture.TimeProvider.GetUtcNow().AddMinutes(15)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
