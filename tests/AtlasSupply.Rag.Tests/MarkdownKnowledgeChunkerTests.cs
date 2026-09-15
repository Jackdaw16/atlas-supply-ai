using AtlasSupply.Infrastructure.Knowledge;
using Xunit;

namespace AtlasSupply.Rag.Tests;

public sealed class MarkdownKnowledgeChunkerTests
{
    [Fact]
    public void Chunk_NormalizesNewlinesAndProducesStableChunks()
    {
        const string lf = "# Policy\n\n## Rules\n\nFirst paragraph.\n\nSecond paragraph.";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        var chunker = new MarkdownKnowledgeChunker();

        var lfChunks = chunker.Chunk(lf);
        var crlfChunks = chunker.Chunk(crlf);

        Assert.Equal(lfChunks, crlfChunks);
        var chunk = Assert.Single(lfChunks);
        Assert.Equal("Policy > Rules", chunk.Heading);
        Assert.Equal("Context: Policy > Rules\n\nFirst paragraph.\n\nSecond paragraph.", chunk.Content);
    }

    [Fact]
    public void Chunk_RespectsMaximumLengthAndProducesStableHashes()
    {
        var markdown = $"# Policy\n\n{new string('a', MarkdownKnowledgeChunker.MaximumChunkLength * 2)}";
        var chunker = new MarkdownKnowledgeChunker();

        var first = chunker.Chunk(markdown);
        var second = chunker.Chunk(markdown);

        Assert.All(first, chunk => Assert.InRange(chunk.Content.Length, 1, MarkdownKnowledgeChunker.MaximumChunkLength));
        Assert.Equal(first, second);
        Assert.Equal(Enumerable.Range(0, first.Count), first.Select(chunk => chunk.Ordinal));
        Assert.All(first, chunk => Assert.Equal(
            MarkdownKnowledgeChunker.ComputeContentHash(chunk.Content),
            chunk.ContentHash));
    }
}
