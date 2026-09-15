using AtlasSupply.Application;
using AtlasSupply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AtlasSupply.Infrastructure.Knowledge;

public sealed class KnowledgeChunkSearch(AtlasSupplyDbContext dbContext) : IKnowledgeChunkSearch
{
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        if (queryEmbedding.Length != OpenAIEmbeddingService.ExpectedDimensions)
        {
            throw new InvalidOperationException(
                $"Knowledge retrieval requires a {OpenAIEmbeddingService.ExpectedDimensions}-dimension embedding.");
        }

        var vector = new Vector(queryEmbedding.ToArray());
        var chunks = await CreateOrderedSearchQuery(vector, topK)
            .Select(chunk => new
            {
                chunk.Document.SourcePath,
                DocumentContentHash = chunk.Document.ContentHash,
                ChunkOrdinal = chunk.Ordinal,
                chunk.Heading,
                chunk.Content,
                Distance = chunk.Embedding.L2Distance(vector)
            })
            .ToListAsync(cancellationToken);

        return chunks
            .Select(chunk => new KnowledgeSearchResult(
                chunk.SourcePath,
                chunk.DocumentContentHash,
                chunk.ChunkOrdinal,
                chunk.Heading,
                chunk.Content,
                chunk.Distance))
            .ToList();
    }

    internal IQueryable<Persistence.Knowledge.KnowledgeChunk> CreateOrderedSearchQuery(
        Vector vector,
        int topK) =>
        dbContext.KnowledgeChunks
            .AsNoTracking()
            .OrderBy(chunk => chunk.Embedding.L2Distance(vector))
            .ThenBy(chunk => chunk.Document.SourcePath)
            .ThenBy(chunk => chunk.Ordinal)
            .Take(topK);
}
