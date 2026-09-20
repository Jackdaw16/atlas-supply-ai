namespace AtlasSupply.Domain;

public sealed record AgentCapabilityScope
{
    private AgentCapabilityScope(string value)
    {
        Value = value;
    }

    public static AgentCapabilityScope SuppliersList { get; } = new("suppliers.list");

    public static AgentCapabilityScope SuppliersRead { get; } = new("suppliers.read");

    public static AgentCapabilityScope OrdersDelayedRead { get; } = new("orders.delayed.read");

    public static AgentCapabilityScope IncidentsCreate { get; } = new("incidents.create");

    public static AgentCapabilityScope KnowledgeSearch { get; } = new("knowledge.search");

    private static readonly IReadOnlyList<AgentCapabilityScope> DefinedScopes =
    [
        SuppliersList,
        SuppliersRead,
        OrdersDelayedRead,
        IncidentsCreate,
        KnowledgeSearch
    ];

    public static IReadOnlyList<AgentCapabilityScope> All => DefinedScopes;

    public string Value { get; }

    public static AgentCapabilityScope FromValue(string value)
    {
        return TryFromValue(value, out var scope)
            ? scope!
            : throw new ArgumentException("The agent capability scope is not defined.", nameof(value));
    }

    public static bool TryFromValue(string? value, out AgentCapabilityScope? scope)
    {
        scope = string.IsNullOrWhiteSpace(value)
            ? null
            : DefinedScopes.SingleOrDefault(candidate =>
                string.Equals(candidate.Value, value.Trim(), StringComparison.Ordinal));

        return scope is not null;
    }
}
