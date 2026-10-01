namespace AtlasSupply.Api;

public sealed record PurchaseOrderItemRequest(string? Description, int? Quantity, decimal? UnitPrice);

public sealed record CreatePurchaseOrderRequest(Guid? SupplierId, IReadOnlyList<PurchaseOrderItemRequest>? Items);

public sealed record UpdatePurchaseOrderItemsRequest(IReadOnlyList<PurchaseOrderItemRequest>? Items);
