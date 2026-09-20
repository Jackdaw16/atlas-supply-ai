namespace AtlasSupply.Domain;

public sealed class UserScopeAssignment
{
    private UserScopeAssignment()
    {
        Scope = null!;
    }

    internal UserScopeAssignment(Guid userId, AgentCapabilityScope scope)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(scope);

        UserId = userId;
        Scope = scope;
    }

    public Guid UserId { get; private set; }

    public AgentCapabilityScope Scope { get; private set; }
}
