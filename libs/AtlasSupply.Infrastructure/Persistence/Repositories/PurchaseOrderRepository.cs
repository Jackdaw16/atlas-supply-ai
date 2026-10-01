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
            .Include(purchaseOrder => purchaseOrder.Items)
            .SingleOrDefaultAsync(purchaseOrder => purchaseOrder.Id == id, cancellationToken);
    }

    public Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.PurchaseOrders
            .Include(purchaseOrder => purchaseOrder.Items)
            .SingleOrDefaultAsync(purchaseOrder => purchaseOrder.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(purchaseOrder => purchaseOrder.Items)
            .OrderByDescending(purchaseOrder => purchaseOrder.CreatedAtUtc)
            .ThenBy(purchaseOrder => purchaseOrder.Id)
            .ToListAsync(cancellationToken);
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

    public async Task CreateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken)
    {
        await dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
