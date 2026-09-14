using AtlasSupply.Domain;

namespace AtlasSupply.Api;

public sealed record CreateIncidentRequest(
    IncidentType? Type,
    string? Description,
    Guid? SupplierId,
    Guid? PurchaseOrderId);
