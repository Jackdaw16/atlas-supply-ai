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
