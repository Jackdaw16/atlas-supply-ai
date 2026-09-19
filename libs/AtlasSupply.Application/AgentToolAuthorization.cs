using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public sealed class AgentAuthorizationContext
{
    public const string ScopeClaimType = "scope";

    private readonly HashSet<AgentCapabilityScope> _capabilities;

    public AgentAuthorizationContext(
        Guid userId,
        string username,
        IEnumerable<AgentCapabilityScope> capabilities)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated user id is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(capabilities);
        UserId = userId;
        Username = username.Trim();
        _capabilities = [.. capabilities];
    }

    public Guid UserId { get; }

    public string Username { get; }

    public bool HasCapability(AgentCapabilityScope capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return _capabilities.Contains(capability);
    }

    public static AgentAuthorizationContext FromValidatedScopeClaimValues(
        Guid userId,
        string username,
        IEnumerable<string> claimValues)
    {
        ArgumentNullException.ThrowIfNull(claimValues);

        var capabilities = new List<AgentCapabilityScope>();
        foreach (var value in claimValues.SelectMany(static claimValue =>
                     claimValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (AgentCapabilityScope.TryFromValue(value, out var capability) && capability is not null)
            {
                capabilities.Add(capability);
            }
        }

        return new AgentAuthorizationContext(userId, username, capabilities);
    }
}

public static class AgentToolCapabilityMap
{
    public const string SearchKnowledgeToolName = "search_knowledge";
    public const string ListSuppliersToolName = "list_suppliers";
    public const string GetSupplierToolName = "get_supplier";
    public const string GetDelayedOrdersToolName = "get_delayed_orders";
    public const string CreateIncidentToolName = "create_incident";

    private static readonly IReadOnlyDictionary<string, AgentCapabilityScope> RequiredCapabilities =
        new Dictionary<string, AgentCapabilityScope>(StringComparer.Ordinal)
        {
            [SearchKnowledgeToolName] = AgentCapabilityScope.KnowledgeSearch,
            [ListSuppliersToolName] = AgentCapabilityScope.SuppliersList,
            [GetSupplierToolName] = AgentCapabilityScope.SuppliersRead,
            [GetDelayedOrdersToolName] = AgentCapabilityScope.OrdersDelayedRead,
            [CreateIncidentToolName] = AgentCapabilityScope.IncidentsCreate
        };

    public static bool IsAuthorized(string? toolName, AgentAuthorizationContext authorizationContext)
    {
        ArgumentNullException.ThrowIfNull(authorizationContext);

        return TryGetRequiredCapability(toolName, out var capability) &&
            authorizationContext.HasCapability(capability);
    }

    public static bool TryGetRequiredCapability(string? toolName, out AgentCapabilityScope capability)
    {
        if (!string.IsNullOrWhiteSpace(toolName) &&
            RequiredCapabilities.TryGetValue(toolName, out var requiredCapability))
        {
            capability = requiredCapability;
            return true;
        }

        capability = null!;
        return false;
    }
}
