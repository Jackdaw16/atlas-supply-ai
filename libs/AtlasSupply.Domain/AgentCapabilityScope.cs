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
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return DefinedScopes.SingleOrDefault(scope =>
            string.Equals(scope.Value, value.Trim(), StringComparison.Ordinal))
            ?? throw new ArgumentException("The agent capability scope is not defined.", nameof(value));
    }
}
