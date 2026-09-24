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
        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI Gateway evaluation failed with HTTP status {(int)response.StatusCode}.");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        stopwatch.Stop();

        var decision = ParseDecision(responseContent, validatedRequest.Criteria, stopwatch.ElapsedMilliseconds);
        logger.LogInformation(
            "Jev agent router selected route {SelectedRoute} with probability {SelectedProbability} in {ElapsedMilliseconds} ms.",
            decision.SelectedRoute,
            decision.SelectedProbability,
            decision.Telemetry.ElapsedMilliseconds);
        return decision;
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
        input = request.Message,
        questions = new[]
        {
            new
            {
                name = "route",
                type = "choice",
                question = "Which route should handle this user message?",
                criteria = request.AvailableCriteria.Select(name => new
                {
                    name,
                    description = name == AgentRoute.General
                        ? "Handle the message without a submitted tool."
                        : $"Route the message to the submitted tool '{name}'."
                })
            }
        },
        providerOptions = new
        {
            gateway = new
            {
                zeroDataRetention = true,
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
                throw MalformedResponse("the root must be a JSON object");
            }

            var routeAnswer = GetRequiredObject(root, "answers", "answers");
            routeAnswer = GetRequiredObject(routeAnswer, "route", "answers.route");
            var selectedRoute = GetRequiredString(routeAnswer, "choice", "answers.route.choice");
            if (!criteria.Contains(selectedRoute))
            {
                throw MalformedResponse("answers.route.choice is not an available route");
            }

            var probabilitiesElement = GetRequiredObject(
                routeAnswer,
                "probabilities",
                "answers.route.probabilities");
            var probabilities = ParseProbabilities(probabilitiesElement);
            if (!probabilities.TryGetValue(selectedRoute, out var selectedProbability))
            {
                throw MalformedResponse("answers.route.probabilities does not include the selected route");
            }

            var telemetry = new AgentRoutingTelemetry(
                GetOptionalString(GetOptionalObject(root, "providerMetadata")?.GetPropertyOrNull("gateway"), "provider"),
                GetOptionalString(root, "model"),
                GetOptionalNonNegativeInt(GetOptionalObject(root, "usage"), "inputTokens"),
                GetOptionalNonNegativeInt(GetOptionalObject(root, "usage"), "outputTokens"),
                GetOptionalCost(root),
                elapsedMilliseconds);

            return new AgentRoutingDecision(selectedRoute, selectedProbability, probabilities, telemetry);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("AI Gateway evaluation returned malformed JSON.", exception);
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
                throw MalformedResponse("answers.route.probabilities contains an invalid probability");
            }
        }

        return probabilities;
    }

    private static decimal? GetOptionalCost(JsonElement root)
    {
        var gatewayMetadata = GetOptionalObject(root, "providerMetadata")?.GetPropertyOrNull("gateway");
        if (gatewayMetadata is null || !gatewayMetadata.Value.TryGetProperty("cost", out var cost))
        {
            return null;
        }

        if (!TryGetDecimal(cost, out var parsedCost) || parsedCost < 0)
        {
            throw MalformedResponse("providerMetadata.gateway.cost is invalid");
        }

        return parsedCost;
    }

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw MalformedResponse($"{path} must be a JSON object");
        }

        return value;
    }

    private static JsonElement? GetOptionalObject(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : null;
    }

    private static string GetRequiredString(JsonElement parent, string propertyName, string path)
    {
        var value = GetOptionalString(parent, propertyName);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw MalformedResponse($"{path} must be a non-empty string");
    }

    private static string? GetOptionalString(JsonElement? parent, string propertyName)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } ||
            !parent.Value.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static int? GetOptionalNonNegativeInt(JsonElement? parent, string propertyName)
    {
        if (parent is null || !parent.Value.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (!value.TryGetInt32(out var parsedValue) || parsedValue < 0)
        {
            throw MalformedResponse($"usage.{propertyName} is invalid");
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

    private static InvalidOperationException MalformedResponse(string detail) =>
        new($"AI Gateway evaluation returned a malformed response: {detail}.");

    private sealed record ValidatedRoutingRequest(string Message, IReadOnlyList<string> AvailableCriteria)
    {
        public IReadOnlySet<string> Criteria { get; } = new HashSet<string>(AvailableCriteria, StringComparer.Ordinal);
    }
}

internal static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement value, string propertyName)
    {
        return value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(propertyName, out var property)
            ? property
            : null;
    }
}
