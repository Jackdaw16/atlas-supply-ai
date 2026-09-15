using AtlasSupply.Application;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;

namespace AtlasSupply.Infrastructure.Knowledge;

public sealed class OpenAIEmbeddingService : IEmbeddingService
{
    public const int ExpectedDimensions = 1_536;

    private readonly EmbeddingClient _embeddingClient;

    public OpenAIEmbeddingService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI embedding API key is missing. Configure OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        var model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("OpenAI embedding model cannot be empty.");
        }

        _embeddingClient = new EmbeddingClient(model, apiKey);
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string input,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Embedding input is required.", nameof(input));
        }

        var result = await _embeddingClient.GenerateEmbeddingAsync(
            input,
            cancellationToken: cancellationToken);
        var embedding = result.Value.ToFloats();
        if (embedding.Length != ExpectedDimensions)
        {
            throw new InvalidOperationException(
                $"OpenAI embedding model returned {embedding.Length} dimensions; expected {ExpectedDimensions}.");
        }

        return embedding;
    }
}
