using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AtlasSupply.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AtlasSupply.Infrastructure.Agents;

public sealed class JevAgentRouter(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<JevAgentRouter> logger) : IAgentRouter
{
    public const string HttpClientName = "AtlasSupply.JevRouting";

    private const string DefaultModel = "typesafe-ai/jev";
    private const int MaximumProviderResponseBodyLength = 1_000;

    public async Task<AgentRoutingDecision> RouteAsync(
        AgentRoutingRequest request,
        CancellationToken cancellationToken)
    {
        var validatedRequest = ValidateRequest(request);
        var apiKey = configuration["AI_GATEWAY_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "AI Gateway API key is missing. Configure AI_GATEWAY_API_KEY before invoking agent routing.");
        }

        var stopwatch = Stopwatch.StartNew();
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/evaluate")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(CreateEvaluationRequest(validatedRequest, GetModel())),
                Encoding.UTF8,
                "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AgentRoutingProviderException(
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    TruncateProviderResponseBody(responseContent));
            }

            stopwatch.Stop();

            var decision = ParseDecision(responseContent, validatedRequest.Criteria, stopwatch.ElapsedMilliseconds);
            logger.LogInformation(
                "Jev agent router selected route {SelectedRoute} with probability {SelectedProbability} in {ElapsedMilliseconds} ms.",
                decision.SelectedRoute,
                decision.SelectedProbability,
                decision.Telemetry.ElapsedMilliseconds);
            return decision;
        }
        catch (HttpRequestException exception)
        {
            throw new AgentRoutingProviderException(
                statusCode: null,
                reasonPhrase: null,
                responseBody: null,
                new HttpRequestException("Agent routing provider transport request failed.", exception));
        }
    }

    private string GetModel() => configuration["AgentRouting:Model"]?.Trim() switch
    {
        { Length: > 0 } model => model,
        _ => DefaultModel
    };

    private static ValidatedRoutingRequest ValidateRequest(AgentRoutingRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Agent routing message is required.", nameof(request));
        }

        if (request.AvailableTools is null || request.AvailableTools.Count == 0)
        {
            throw new ArgumentException("At least one available tool is required for agent routing.", nameof(request));
        }

        var tools = new List<string>(request.AvailableTools.Count);
        var toolNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var toolName in request.AvailableTools)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentException("Agent routing tool names cannot be empty.", nameof(request));
            }

            if (string.Equals(toolName, AgentRoute.General, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"'{AgentRoute.General}' is reserved for general agent routing.",
                    nameof(request));
            }

            if (!toolNames.Add(toolName))
            {
                throw new ArgumentException("Agent routing tool names must be unique.", nameof(request));
            }

            tools.Add(toolName);
        }

        tools.Add(AgentRoute.General);
        return new ValidatedRoutingRequest(request.Message.Trim(), tools);
    }

    private static object CreateEvaluationRequest(ValidatedRoutingRequest request, string model) => new
    {
        model,
        state = request.Message,
        questions = new Dictionary<string, object>
        {
            ["route"] = new
            {
                type = "choice",
                instructions = "Which route should handle this user message?",
                criteria = request.AvailableCriteria.ToDictionary(
                    name => name,
                    name => name == AgentRoute.General
                        ? "Handle the message without a submitted tool."
                        : $"Route the message to the submitted tool '{name}'.",
                    StringComparer.Ordinal)
            }
        },
        providerOptions = new
        {
            gateway = new
            {
                only = new[] { "typesafe-ai" }
            }
        }
    };

    private static AgentRoutingDecision ParseDecision(
        string responseContent,
        IReadOnlySet<string> criteria,
        long elapsedMilliseconds)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw MalformedResponse("response", "must be a JSON object");
            }

            var routeAnswer = GetRequiredObject(root, "answers", "answers");
            routeAnswer = GetRequiredObject(routeAnswer, "route", "answers.route");
            var selectedRoute = GetRequiredString(routeAnswer, "choice", "answers.route.choice");
            if (!criteria.Contains(selectedRoute))
            {
                throw MalformedResponse("answers.route.choice", "is not an available route");
            }

            var probabilitiesElement = GetRequiredObject(
                routeAnswer,
                "probabilities",
                "answers.route.probabilities");
            var probabilities = ParseProbabilities(probabilitiesElement);
            if (!probabilities.TryGetValue(selectedRoute, out var selectedProbability))
            {
                throw MalformedResponse("answers.route.probabilities", "does not include the selected route");
            }

            var providerMetadata = GetOptionalObject(root, "providerMetadata", "providerMetadata");
            var gatewayMetadata = providerMetadata.HasValue
                ? GetOptionalObject(providerMetadata.Value, "gateway", "providerMetadata.gateway")
                : null;

            var telemetry = new AgentRoutingTelemetry(
                GetOptionalString(gatewayMetadata, "provider", "providerMetadata.gateway.provider"),
                GetOptionalString(root, "model", "model"),
                GetOptionalNonNegativeInt(GetOptionalObject(root, "usage", "usage"), "inputTokens"),
                GetOptionalNonNegativeInt(GetOptionalObject(root, "usage", "usage"), "outputTokens"),
                GetOptionalCost(gatewayMetadata),
                elapsedMilliseconds);

            return new AgentRoutingDecision(selectedRoute, selectedProbability, probabilities, telemetry);
        }
        catch (JsonException exception)
        {
            throw MalformedResponse("response", "is not valid JSON", exception);
        }
    }

    private static Dictionary<string, decimal> ParseProbabilities(JsonElement probabilitiesElement)
    {
        var probabilities = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var probability in probabilitiesElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(probability.Name) ||
                !TryGetDecimal(probability.Value, out var value) ||
                value is < 0 or > 1 ||
                !probabilities.TryAdd(probability.Name, value))
            {
                throw MalformedResponse("answers.route.probabilities", "contains an invalid probability");
            }
        }

        return probabilities;
    }

    private static decimal? GetOptionalCost(JsonElement? gatewayMetadata)
    {
        if (gatewayMetadata is null || !gatewayMetadata.Value.TryGetProperty("cost", out var cost))
        {
            return null;
        }

        if (!TryGetDecimal(cost, out var parsedCost) || parsedCost < 0)
        {
            throw MalformedResponse("providerMetadata.gateway.cost", "is invalid");
        }

        return parsedCost;
    }

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw MalformedResponse(path, "must be a JSON object");
        }

        return value;
    }

    private static JsonElement? GetOptionalObject(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Object
            ? value
            : throw MalformedResponse(path, "must be a JSON object");
    }

    private static string GetRequiredString(JsonElement parent, string propertyName, string path)
    {
        var value = GetOptionalString(parent, propertyName, path);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw MalformedResponse(path, "must be a non-empty string");
    }

    private static string? GetOptionalString(JsonElement? parent, string propertyName, string path)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } ||
            !parent.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw MalformedResponse(path, "must be a string");
    }

    private static int? GetOptionalNonNegativeInt(JsonElement? parent, string propertyName)
    {
        if (parent is null || !parent.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (!value.TryGetInt32(out var parsedValue) || parsedValue < 0)
        {
            throw MalformedResponse($"usage.{propertyName}", "is invalid");
        }

        return parsedValue;
    }

    private static bool TryGetDecimal(JsonElement value, out decimal parsedValue)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString(),
            _ => null
        };

        return decimal.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out parsedValue);
    }

    private static string? TruncateProviderResponseBody(string responseBody) =>
        responseBody.Length <= MaximumProviderResponseBodyLength
            ? responseBody
            : responseBody[..MaximumProviderResponseBodyLength];

    private static AgentRoutingResponseException MalformedResponse(
        string fieldPath,
        string detail,
        Exception? innerException = null) =>
        new(fieldPath, detail, innerException);

    private sealed record ValidatedRoutingRequest(string Message, IReadOnlyList<string> AvailableCriteria)
    {
        public IReadOnlySet<string> Criteria { get; } = new HashSet<string>(AvailableCriteria, StringComparer.Ordinal);
    }
}
