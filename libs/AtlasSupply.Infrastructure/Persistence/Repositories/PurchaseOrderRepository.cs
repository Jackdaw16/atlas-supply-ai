using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.EntityFrameworkCore;

namespace AtlasSupply.Infrastructure.Persistence.Repositories;

public sealed class PurchaseOrderRepository(AtlasSupplyDbContext dbContext) : IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.PurchaseOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(purchaseOrder => purchaseOrder.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> ListOutstandingApprovedAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(purchaseOrder =>
                purchaseOrder.Status == PurchaseOrderStatus.Approved &&
                purchaseOrder.ApprovedAtUtc != null &&
                purchaseOrder.ReceivedAtUtc == null)
            .OrderBy(purchaseOrder => purchaseOrder.ApprovedAtUtc)
            .ThenBy(purchaseOrder => purchaseOrder.Id)
            .ToListAsync(cancellationToken);
    }
}
