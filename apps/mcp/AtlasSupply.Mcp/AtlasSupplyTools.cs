using System.ComponentModel;
using AtlasSupply.Application;
using AtlasSupply.Domain;
using ModelContextProtocol.Server;

namespace AtlasSupply.Mcp;

public sealed record ListSuppliersToolResult(IReadOnlyList<SupplierResult> Suppliers);

public sealed record GetSupplierToolResult(bool Found, SupplierResult? Supplier);

public sealed record GetDelayedOrdersToolResult(IReadOnlyList<DelayedOrderResult> Orders);

public sealed record CreateIncidentToolResult(CreateIncidentResult Incident);

[McpServerToolType]
public sealed class AtlasSupplyTools
{
    [McpServerTool(
        Name = "list_suppliers",
        Destructive = false,
        ReadOnly = true,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(ListSuppliersToolResult))]
    [Description("Lists all suppliers with their contact details and active status.")]
    public static async Task<ListSuppliersToolResult> ListSuppliersAsync(
        ListSuppliers listSuppliers,
        CancellationToken cancellationToken)
    {
        var suppliers = await listSuppliers.ExecuteAsync(cancellationToken);
        return new ListSuppliersToolResult(suppliers);
    }

    [McpServerTool(
        Name = "get_supplier",
        Destructive = false,
        ReadOnly = true,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GetSupplierToolResult))]
    [Description("Gets a supplier by ID. Returns found as false when no supplier has that ID.")]
    public static async Task<GetSupplierToolResult> GetSupplierAsync(
        [Description("The supplier's non-empty UUID.")] Guid supplierId,
        GetSupplierById getSupplierById,
        CancellationToken cancellationToken)
    {
        var supplier = await getSupplierById.ExecuteAsync(
            new GetSupplierByIdInput(supplierId),
            cancellationToken);

        return new GetSupplierToolResult(supplier is not null, supplier);
    }

    [McpServerTool(
        Name = "get_delayed_orders",
        Destructive = false,
        ReadOnly = true,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GetDelayedOrdersToolResult))]
    [Description("Lists approved purchase orders that have not been received yet.")]
    public static async Task<GetDelayedOrdersToolResult> GetDelayedOrdersAsync(
        GetDelayedOrders getDelayedOrders,
        CancellationToken cancellationToken)
    {
        var orders = await getDelayedOrders.ExecuteAsync(cancellationToken);
        return new GetDelayedOrdersToolResult(orders);
    }

    [McpServerTool(
        Name = "create_incident",
        UseStructuredContent = true,
        OutputSchemaType = typeof(CreateIncidentToolResult))]
    [Description("Creates an incident for a supplier and optionally links it to one of that supplier's purchase orders.")]
    public static async Task<CreateIncidentToolResult> CreateIncidentAsync(
        [Description("The incident category: Delay, QualityIssue, ShortShipment, DamagedGoods, or Other.")] IncidentType type,
        [Description("A concise description of the incident, up to 2000 characters.")] string description,
        [Description("The affected supplier's non-empty UUID.")] Guid supplierId,
        [Description("The related purchase order's UUID, if applicable.")] Guid? purchaseOrderId,
        CreateIncident createIncident,
        CancellationToken cancellationToken)
    {
        var incident = await createIncident.ExecuteAsync(
            new CreateIncidentInput(type, description, supplierId, purchaseOrderId),
            cancellationToken);

        return new CreateIncidentToolResult(incident);
    }
}
