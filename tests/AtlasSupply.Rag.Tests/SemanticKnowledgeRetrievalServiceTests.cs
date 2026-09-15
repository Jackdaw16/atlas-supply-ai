using AtlasSupply.Application;
using Xunit;

namespace AtlasSupply.Rag.Tests;

public sealed class SemanticKnowledgeRetrievalServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task SearchAsync_RejectsInvalidTopK(int topK)
    {
        var service = new SemanticKnowledgeRetrievalService(
            new ThrowingEmbeddingService(),
            new ThrowingKnowledgeChunkSearch());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SearchAsync(
            new KnowledgeSearchInput("policy", topK),
            CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_RejectsWhitespaceQuery()
    {
        var service = new SemanticKnowledgeRetrievalService(
            new ThrowingEmbeddingService(),
            new ThrowingKnowledgeChunkSearch());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(
            new KnowledgeSearchInput("   "),
            CancellationToken.None));
    }

    private sealed class ThrowingEmbeddingService : IEmbeddingService
    {
        public Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string input, CancellationToken cancellationToken)
        {
            throw new Xunit.Sdk.XunitException("Embedding should not be requested for invalid input.");
        }
    }

    private sealed class ThrowingKnowledgeChunkSearch : IKnowledgeChunkSearch
    {
        public Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
            ReadOnlyMemory<float> queryEmbedding,
            int topK,
            CancellationToken cancellationToken)
        {
            throw new Xunit.Sdk.XunitException("Search should not be requested for invalid input.");
        }
    }
}
