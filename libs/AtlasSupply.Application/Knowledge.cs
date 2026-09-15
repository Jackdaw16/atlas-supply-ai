namespace AtlasSupply.Application;

public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken);
}

public sealed record KnowledgeSearchInput(string Query, int TopK = 5);

// Distance is Euclidean (L2); smaller values represent closer matches.
public sealed record KnowledgeSearchResult(
    string SourcePath,
    string DocumentContentHash,
    int ChunkOrdinal,
    string? Heading,
    string Content,
    double Distance);

public interface IKnowledgeChunkSearch
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken);
}

public interface IKnowledgeRetrievalService
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        KnowledgeSearchInput input,
        CancellationToken cancellationToken);
}

public sealed class SemanticKnowledgeRetrievalService(
    IEmbeddingService embeddingService,
    IKnowledgeChunkSearch knowledgeChunkSearch) : IKnowledgeRetrievalService
{
    private const int MaximumTopK = 20;
    private const int MaximumQueryLength = 4_000;

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        KnowledgeSearchInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Query))
        {
            throw new ArgumentException("Knowledge search query is required.", nameof(input));
        }

        if (input.Query.Length > MaximumQueryLength)
        {
            throw new ArgumentException(
                $"Knowledge search query cannot exceed {MaximumQueryLength} characters.",
                nameof(input));
        }

        if (input.TopK is < 1 or > MaximumTopK)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                input.TopK,
                $"TopK must be between 1 and {MaximumTopK}.");
        }

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(
            input.Query.Trim(),
            cancellationToken);

        return await knowledgeChunkSearch.SearchAsync(
            queryEmbedding,
            input.TopK,
            cancellationToken);
    }
}

public sealed record KnowledgeIngestionResult(
    int DiscoveredDocuments,
    int AddedDocuments,
    int UpdatedDocuments,
    int UnchangedDocuments,
    int RemovedDocuments,
    int AddedChunks,
    int RemovedChunks);

public interface IKnowledgeIngestionService
{
    Task<KnowledgeIngestionResult> IngestAsync(CancellationToken cancellationToken);
}
