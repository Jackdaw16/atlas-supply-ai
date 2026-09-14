using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseOrder>> ListOutstandingApprovedAsync(CancellationToken cancellationToken);
}

public sealed record DelayedOrderResult(
    Guid Id,
    Guid SupplierId,
    DateTime ApprovedAtUtc);

public sealed class GetDelayedOrders(IPurchaseOrderRepository purchaseOrderRepository)
{
    public async Task<IReadOnlyList<DelayedOrderResult>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var purchaseOrders = await purchaseOrderRepository.ListOutstandingApprovedAsync(cancellationToken);

        return purchaseOrders
            .Select(static purchaseOrder => new DelayedOrderResult(
                purchaseOrder.Id,
                purchaseOrder.SupplierId,
                purchaseOrder.ApprovedAtUtc!.Value))
            .ToArray();
    }
}
