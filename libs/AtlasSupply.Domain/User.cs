namespace AtlasSupply.Domain;

public sealed class User
{
    private readonly List<UserScopeAssignment> _scopeAssignments = [];

    private User()
    {
    }

    public User(Guid id, string username, string email, string passwordHash)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(id));
        }

        Id = id;
        SetUsername(username);
        SetEmail(email);
        SetPasswordHash(passwordHash);
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public IReadOnlyCollection<UserScopeAssignment> ScopeAssignments => _scopeAssignments.AsReadOnly();

    public void SetUsername(string username)
    {
        var normalizedUsername = RequireValue(username, "Username", 100);
        Username = normalizedUsername;
        NormalizedUsername = normalizedUsername.ToUpperInvariant();
    }

    public void SetEmail(string email)
    {
        var normalizedEmail = RequireValue(email, "Email", 320);

        if (!normalizedEmail.Contains('@'))
        {
            throw new ArgumentException("Email is invalid.", nameof(email));
        }

        Email = normalizedEmail;
        NormalizedEmail = normalizedEmail.ToUpperInvariant();
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = RequireValue(passwordHash, "Password hash", 512);
    }

    public void AssignScope(AgentCapabilityScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (_scopeAssignments.All(assignment => assignment.Scope != scope))
        {
            _scopeAssignments.Add(new UserScopeAssignment(Id, scope));
        }
    }

    private static string RequireValue(string value, string valueName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException($"{valueName} exceeds the maximum length of {maximumLength}.", valueName);
        }

        return normalizedValue;
    }
}
