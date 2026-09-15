using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AtlasSupply.Infrastructure.Knowledge;

public sealed record MarkdownKnowledgeChunk(
    int Ordinal,
    string? Heading,
    string Content,
    string ContentHash);

public interface IMarkdownKnowledgeChunker
{
    IReadOnlyList<MarkdownKnowledgeChunk> Chunk(string markdown);
}

public sealed partial class MarkdownKnowledgeChunker : IMarkdownKnowledgeChunker
{
    public const int MaximumChunkLength = 1_200;

    private const int MaximumHeadingLength = 256;

    public IReadOnlyList<MarkdownKnowledgeChunk> Chunk(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var normalizedMarkdown = NormalizeNewlines(markdown);
        if (string.IsNullOrWhiteSpace(normalizedMarkdown))
        {
            return [];
        }

        var sections = new List<(string? Heading, List<string> Paragraphs)>();
        var headings = new List<string>();
        var paragraphs = new List<string>();
        var paragraphLines = new List<string>();

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            paragraphs.Add(string.Join("\n", paragraphLines).Trim());
            paragraphLines.Clear();
        }

        void FlushSection()
        {
            FlushParagraph();
            if (paragraphs.Count == 0)
            {
                return;
            }

            sections.Add((headings.Count == 0 ? null : string.Join(" > ", headings), [.. paragraphs]));
            paragraphs.Clear();
        }

        foreach (var line in normalizedMarkdown.Split('\n'))
        {
            var headingMatch = HeadingExpression().Match(line);
            if (headingMatch.Success)
            {
                FlushSection();

                var level = headingMatch.Groups[1].Value.Length;
                while (headings.Count >= level)
                {
                    headings.RemoveAt(headings.Count - 1);
                }

                headings.Add(headingMatch.Groups[2].Value.Trim());
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }

            paragraphLines.Add(line.TrimEnd());
        }

        FlushSection();

        var chunks = new List<MarkdownKnowledgeChunk>();
        foreach (var section in sections)
        {
            AddSectionChunks(section.Heading, section.Paragraphs, chunks);
        }

        return chunks
            .Select((chunk, ordinal) => chunk with
            {
                Ordinal = ordinal,
                ContentHash = ComputeContentHash(chunk.Content)
            })
            .ToArray();
    }

    public static string NormalizeNewlines(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    public static string ComputeContentHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static void AddSectionChunks(
        string? heading,
        IReadOnlyList<string> paragraphs,
        ICollection<MarkdownKnowledgeChunk> chunks)
    {
        var normalizedHeading = NormalizeHeading(heading);
        var prefix = normalizedHeading is null ? string.Empty : $"Context: {normalizedHeading}\n\n";
        var payloadCapacity = MaximumChunkLength - prefix.Length;
        if (payloadCapacity < 1)
        {
            throw new InvalidOperationException("Knowledge chunk heading leaves no room for content.");
        }

        var buffer = new StringBuilder();

        void FlushChunk()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            chunks.Add(new MarkdownKnowledgeChunk(0, normalizedHeading, prefix + buffer, string.Empty));
            buffer.Clear();
        }

        foreach (var paragraph in paragraphs)
        {
            var remaining = paragraph;
            while (remaining.Length > payloadCapacity)
            {
                FlushChunk();
                var length = FindBreakLength(remaining, payloadCapacity);
                chunks.Add(new MarkdownKnowledgeChunk(
                    0,
                    normalizedHeading,
                    prefix + remaining[..length].TrimEnd(),
                    string.Empty));
                remaining = remaining[length..].TrimStart();
            }

            if (buffer.Length == 0)
            {
                buffer.Append(remaining);
            }
            else if (buffer.Length + 2 + remaining.Length <= payloadCapacity)
            {
                buffer.Append("\n\n").Append(remaining);
            }
            else
            {
                FlushChunk();
                buffer.Append(remaining);
            }
        }

        FlushChunk();
    }

    private static int FindBreakLength(string value, int maximumLength)
    {
        var length = maximumLength;
        while (length > 1 && !char.IsWhiteSpace(value[length - 1]))
        {
            length--;
        }

        return length == 1 ? maximumLength : length;
    }

    private static string? NormalizeHeading(string? heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return null;
        }

        var normalized = heading.Trim();
        return normalized.Length <= MaximumHeadingLength
            ? normalized
            : string.Concat(normalized.AsSpan(0, MaximumHeadingLength - 3), "...");
    }

    [GeneratedRegex("^(#{1,6})\\s+(.+?)\\s*#*\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingExpression();
}
