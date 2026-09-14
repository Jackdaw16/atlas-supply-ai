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
}
