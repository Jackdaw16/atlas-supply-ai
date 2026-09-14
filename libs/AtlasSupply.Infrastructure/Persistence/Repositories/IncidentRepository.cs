using AtlasSupply.Application;
using AtlasSupply.Domain;

namespace AtlasSupply.Infrastructure.Persistence.Repositories;

public sealed class IncidentRepository(AtlasSupplyDbContext dbContext) : IIncidentRepository
{
    public async Task AddAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);

        await dbContext.Incidents.AddAsync(incident, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
