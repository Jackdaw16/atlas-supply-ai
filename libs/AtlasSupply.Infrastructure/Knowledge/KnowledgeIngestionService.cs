using System.Security.Cryptography;
using System.Text;
using AtlasSupply.Application;
using AtlasSupply.Infrastructure.Persistence;
using AtlasSupply.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pgvector;

namespace AtlasSupply.Infrastructure.Knowledge;

public sealed class KnowledgeIngestionOptions
{
    public string SourceDirectory { get; set; } = "docs/knowledge";
}

public sealed class KnowledgeIngestionService(
    AtlasSupplyDbContext dbContext,
    IEmbeddingService embeddingService,
    IMarkdownKnowledgeChunker markdownKnowledgeChunker,
    IOptions<KnowledgeIngestionOptions> options,
    IHostEnvironment hostEnvironment) : IKnowledgeIngestionService
{
    public async Task<KnowledgeIngestionResult> IngestAsync(CancellationToken cancellationToken)
    {
        var repositoryRoot = FindRepositoryRoot(hostEnvironment.ContentRootPath);
        var sourceDirectory = ResolveSourceDirectory(repositoryRoot, options.Value.SourceDirectory);
        var sourcePathPrefix = ToSourcePath(repositoryRoot, sourceDirectory).TrimEnd('/') + "/";
        var files = Directory.EnumerateFiles(sourceDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        var sources = new List<SourceDocument>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var normalizedContent = MarkdownKnowledgeChunker.NormalizeNewlines(content);
            sources.Add(new SourceDocument(
                ToSourcePath(repositoryRoot, file),
                content,
                MarkdownKnowledgeChunker.ComputeContentHash(normalizedContent)));
        }

        var existingDocuments = await dbContext.KnowledgeDocuments
            .Include(document => document.Chunks)
            .Where(document => document.SourcePath.StartsWith(sourcePathPrefix))
            .ToListAsync(cancellationToken);
        var documentsBySourcePath = existingDocuments.ToDictionary(
            document => document.SourcePath,
            StringComparer.Ordinal);
        var currentSourcePaths = sources.Select(source => source.SourcePath).ToHashSet(StringComparer.Ordinal);

        var addedDocuments = 0;
        var updatedDocuments = 0;
        var unchangedDocuments = 0;
        var removedDocuments = 0;
        var addedChunks = 0;
        var removedChunks = 0;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (documentsBySourcePath.TryGetValue(source.SourcePath, out var existingDocument) &&
                existingDocument.ContentHash == source.ContentHash)
            {
                unchangedDocuments++;
                continue;
            }

            var chunks = markdownKnowledgeChunker.Chunk(source.Content);
            var embeddedChunks = new List<(MarkdownKnowledgeChunk Chunk, Vector Embedding)>(chunks.Count);
            foreach (var chunk in chunks)
            {
                var embedding = await embeddingService.GenerateEmbeddingAsync(chunk.Content, cancellationToken);
                embeddedChunks.Add((chunk, new Vector(embedding.ToArray())));
            }

            var now = DateTime.UtcNow;
            if (existingDocument is null)
            {
                existingDocument = new KnowledgeDocument
                {
                    Id = CreateDeterministicGuid($"knowledge-document-v1\n{source.SourcePath}"),
                    SourcePath = source.SourcePath,
                    ContentHash = source.ContentHash,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                dbContext.KnowledgeDocuments.Add(existingDocument);
                addedDocuments++;
            }
            else
            {
                removedChunks += existingDocument.Chunks.Count;
                dbContext.KnowledgeChunks.RemoveRange(existingDocument.Chunks);
                existingDocument.Chunks.Clear();
                existingDocument.ContentHash = source.ContentHash;
                existingDocument.UpdatedAtUtc = now;
                updatedDocuments++;
            }

            foreach (var (chunk, embedding) in embeddedChunks)
            {
                existingDocument.Chunks.Add(new KnowledgeChunk
                {
                    Id = CreateDeterministicGuid(
                        $"knowledge-chunk-v1\n{source.SourcePath}\n{source.ContentHash}\n{chunk.Ordinal}\n{chunk.ContentHash}"),
                    DocumentId = existingDocument.Id,
                    Ordinal = chunk.Ordinal,
                    ContentHash = chunk.ContentHash,
                    Heading = chunk.Heading,
                    Content = chunk.Content,
                    Embedding = embedding,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                addedChunks++;
            }
        }

        foreach (var obsoleteDocument in existingDocuments.Where(document => !currentSourcePaths.Contains(document.SourcePath)))
        {
            removedChunks += obsoleteDocument.Chunks.Count;
            dbContext.KnowledgeDocuments.Remove(obsoleteDocument);
            removedDocuments++;
        }

        if (addedDocuments + updatedDocuments + removedDocuments > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new KnowledgeIngestionResult(
            sources.Count,
            addedDocuments,
            updatedDocuments,
            unchangedDocuments,
            removedDocuments,
            addedChunks,
            removedChunks);
    }

    private static string FindRepositoryRoot(string contentRootPath)
    {
        for (var directory = new DirectoryInfo(contentRootPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtlasSupply.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not resolve the repository root containing AtlasSupply.sln for knowledge ingestion.");
    }

    private static string ResolveSourceDirectory(string repositoryRoot, string configuredSourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredSourceDirectory) || Path.IsPathRooted(configuredSourceDirectory))
        {
            throw new InvalidOperationException("Knowledge:SourceDirectory must be a repository-relative path.");
        }

        var sourceDirectory = Path.GetFullPath(Path.Combine(repositoryRoot, configuredSourceDirectory));
        var rootWithSeparator = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!sourceDirectory.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Knowledge:SourceDirectory must resolve within the repository root.");
        }

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Configured knowledge source directory was not found: {sourceDirectory}");
        }

        return sourceDirectory;
    }

    private static string ToSourcePath(string repositoryRoot, string fullPath)
    {
        return Path.GetRelativePath(repositoryRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record SourceDocument(string SourcePath, string Content, string ContentHash);
}
