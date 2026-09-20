using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface IAgentToolAuditWriter
{
    Task WriteAttemptAsync(AgentToolAuditRecord auditRecord, CancellationToken cancellationToken);

    Task WriteOutcomeAsync(AgentToolAuditRecord auditRecord, CancellationToken cancellationToken);
}

public sealed class AgentToolAuditException(string message, Exception innerException) : Exception(message, innerException);
