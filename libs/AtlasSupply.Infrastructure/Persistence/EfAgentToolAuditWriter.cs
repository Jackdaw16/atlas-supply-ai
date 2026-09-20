using AtlasSupply.Application;
using AtlasSupply.Domain;

namespace AtlasSupply.Infrastructure.Persistence;

public sealed class EfAgentToolAuditWriter(AtlasSupplyDbContext dbContext) : IAgentToolAuditWriter
{
    public async Task WriteAttemptAsync(AgentToolAuditRecord auditRecord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditRecord);

        dbContext.AgentToolAuditRecords.Add(auditRecord);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task WriteOutcomeAsync(AgentToolAuditRecord auditRecord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditRecord);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
