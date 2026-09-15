namespace AtlasSupply.Infrastructure.Persistence.Knowledge;

internal sealed class KnowledgeDocument
{
    public Guid Id { get; set; }

    public string SourcePath { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<KnowledgeChunk> Chunks { get; } = [];
}
