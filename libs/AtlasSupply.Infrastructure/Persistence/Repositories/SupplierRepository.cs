using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.EntityFrameworkCore;

namespace AtlasSupply.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository(AtlasSupplyDbContext dbContext) : ISupplierRepository
{
    public async Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Suppliers
            .AsNoTracking()
            .OrderBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);
    }

    public Task<Supplier?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Suppliers
            .SingleOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);
    }

    public async Task CreateAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        await dbContext.Suppliers.AddAsync(supplier, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
