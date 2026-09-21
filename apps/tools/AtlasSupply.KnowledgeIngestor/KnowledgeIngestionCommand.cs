using AtlasSupply.Application;

namespace AtlasSupply.KnowledgeIngestor;

public sealed class KnowledgeIngestionCommand(
    IKnowledgeIngestionService knowledgeIngestionService,
    TextWriter output,
    TextWriter error)
{
    public async Task<int> ExecuteAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var discoveredFiles = DiscoverMarkdownFiles(sourceDirectory);
            if (discoveredFiles.Count == 0)
            {
                throw new InvalidOperationException("No Markdown knowledge documents were found.");
            }

            var result = await knowledgeIngestionService.IngestAsync(cancellationToken);
            await output.WriteLineAsync("Knowledge ingestion completed.");
            await output.WriteLineAsync($"Documents discovered: {result.DiscoveredDocuments}");
            await output.WriteLineAsync($"Documents inserted: {result.AddedDocuments}");
            await output.WriteLineAsync($"Documents updated: {result.UpdatedDocuments}");
            await output.WriteLineAsync($"Documents unchanged: {result.UnchangedDocuments}");
            await output.WriteLineAsync($"Chunks written: {result.AddedChunks}");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await error.WriteLineAsync($"Knowledge ingestion failed: {exception.GetType().Name}.");
            return 1;
        }
    }

    public static string ParseSourceDirectory(string[] args)
    {
        if (args.Length == 0)
        {
            return "docs/knowledge";
        }

        if (args.Length == 2 && string.Equals(args[0], "--knowledge-directory", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(args[1]))
            {
                throw new ArgumentException("The knowledge directory cannot be empty.");
            }

            return args[1];
        }

        throw new ArgumentException("Usage: --knowledge-directory <repository-relative-path>");
    }

    public static IReadOnlyList<string> DiscoverMarkdownFiles(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException("The knowledge source directory was not found.");
        }

        return Directory.EnumerateFiles(sourceDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }
}
