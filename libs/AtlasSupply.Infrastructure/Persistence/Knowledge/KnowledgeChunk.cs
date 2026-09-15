using Pgvector;

namespace AtlasSupply.Infrastructure.Persistence.Knowledge;

internal sealed class KnowledgeChunk
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public KnowledgeDocument Document { get; set; } = null!;

    public int Ordinal { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public string? Heading { get; set; }

    public string Content { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
