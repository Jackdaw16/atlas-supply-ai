using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.EntityFrameworkCore;

namespace AtlasSupply.Infrastructure.Persistence.Repositories;

public sealed class IncidentRepository(AtlasSupplyDbContext dbContext) : IIncidentRepository
{
    public async Task AddAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);

        await dbContext.Incidents.AddAsync(incident, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Incident>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .ThenBy(incident => incident.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Incident>> PageAsync(IncidentPageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var incidents = dbContext.Incidents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = $"%{request.Search.Trim()}%";
            incidents = incidents.Where(incident =>
                EF.Functions.ILike(incident.Id.ToString(), search)
                || EF.Functions.ILike(incident.Description, search)
                || dbContext.Suppliers.Any(supplier =>
                    supplier.Id == incident.SupplierId && EF.Functions.ILike(supplier.Name, search)));
        }

        if (request.Status is { } status)
        {
            incidents = incidents.Where(incident => incident.Status == status);
        }

        if (request.SupplierId is { } supplierId)
        {
            incidents = incidents.Where(incident => incident.SupplierId == supplierId);
        }

        if (request.Type is { } type)
        {
            incidents = incidents.Where(incident => incident.Type == type);
        }

        if (request.Lifecycle is { } lifecycle)
        {
            incidents = lifecycle == IncidentLifecycleFilter.Open
                ? incidents.Where(incident => incident.Status == IncidentStatus.Open)
                : incidents.Where(incident => incident.Status == IncidentStatus.Resolved);
        }

        var totalCount = await incidents.CountAsync(cancellationToken);
        var items = await incidents
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .ThenBy(incident => incident.Id)
            .Skip(request.Page.PageIndex * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Incident>(items, totalCount);
    }

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(incident => incident.Id == id, cancellationToken);
    }

    public Task<Incident?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Incidents
            .SingleOrDefaultAsync(incident => incident.Id == id, cancellationToken);
    }

    public Task UpdateAsync(Incident incident, CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
