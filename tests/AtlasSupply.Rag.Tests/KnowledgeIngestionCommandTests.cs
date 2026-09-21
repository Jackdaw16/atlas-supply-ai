using AtlasSupply.Application;
using AtlasSupply.KnowledgeIngestor;
using Xunit;

namespace AtlasSupply.Rag.Tests;

public sealed class KnowledgeIngestionCommandTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_WithMarkdownDocuments_ReturnsSuccessAndPrintsSummary()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        await File.WriteAllTextAsync(Path.Combine(_temporaryDirectory, "policy.md"), "# Policy");
        var output = new StringWriter();
        var command = new KnowledgeIngestionCommand(
            new StubKnowledgeIngestionService(new KnowledgeIngestionResult(1, 1, 0, 0, 0, 2, 0)),
            output,
            TextWriter.Null);

        var exitCode = await command.ExecuteAsync(_temporaryDirectory, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Knowledge ingestion completed.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Documents inserted: 1", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Chunks written: 2", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMarkdownDocuments_ReturnsFailureWithoutIngesting()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var service = new StubKnowledgeIngestionService(new KnowledgeIngestionResult(0, 0, 0, 0, 0, 0, 0));
        var error = new StringWriter();
        var command = new KnowledgeIngestionCommand(service, TextWriter.Null, error);

        var exitCode = await command.ExecuteAsync(_temporaryDirectory, CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.False(service.WasCalled);
        Assert.Contains("Knowledge ingestion failed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverMarkdownFiles_OnlyReturnsSupportedDocuments()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        File.WriteAllText(Path.Combine(_temporaryDirectory, "policy.md"), "# Policy");
        File.WriteAllText(Path.Combine(_temporaryDirectory, "notes.txt"), "ignore");

        var files = KnowledgeIngestionCommand.DiscoverMarkdownFiles(_temporaryDirectory);

        Assert.Single(files);
        Assert.EndsWith("policy.md", files[0], StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private sealed class StubKnowledgeIngestionService(KnowledgeIngestionResult result) : IKnowledgeIngestionService
    {
        internal bool WasCalled { get; private set; }

        public Task<KnowledgeIngestionResult> IngestAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }
}
