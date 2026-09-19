using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface IUserCredentialStore
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
}

public interface IPasswordVerifier
{
    bool Verify(User user, string password);
}

public interface IAccessTokenIssuer
{
    Task<IssuedAccessToken> IssueAsync(AccessTokenIssueRequest request, CancellationToken cancellationToken);
}

public sealed record AccessTokenIssueRequest(
    Guid SubjectId,
    string Username,
    IReadOnlyList<string> Scopes);

public sealed record IssuedAccessToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

public sealed record LoginInput(string Username, string Password);

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Username,
    IReadOnlyList<string> Scopes);

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid username or password.")
    {
    }
}

public sealed class Login(
    IUserCredentialStore userCredentialStore,
    IPasswordVerifier passwordVerifier,
    IAccessTokenIssuer accessTokenIssuer)
{
    public async Task<LoginResult> ExecuteAsync(LoginInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrWhiteSpace(input.Password))
        {
            throw new ArgumentException("Username and password are required.", nameof(input));
        }

        var user = await userCredentialStore.FindByUsernameAsync(input.Username, cancellationToken);
        if (user is null || !passwordVerifier.Verify(user, input.Password))
        {
            throw new InvalidCredentialsException();
        }

        var scopes = user.ScopeAssignments
            .Select(static assignment => assignment.Scope.Value)
            .OrderBy(static scope => scope, StringComparer.Ordinal)
            .ToArray();

        var issuedToken = await accessTokenIssuer.IssueAsync(
            new AccessTokenIssueRequest(user.Id, user.Username, scopes),
            cancellationToken);

        return new LoginResult(
            issuedToken.AccessToken,
            issuedToken.ExpiresAtUtc,
            user.Username,
            scopes);
    }
}
