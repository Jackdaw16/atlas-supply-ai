using System.Text.Json;
using AtlasSupply.Application;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AtlasSupply.Infrastructure.Agents;

public sealed class McpAgentToolProvider : IAgentToolProvider
{
    private readonly Uri _endpoint;
    private readonly IMcpIdTokenProvider _idTokenProvider;

    public McpAgentToolProvider(IConfiguration configuration)
        : this(configuration, new GoogleMcpIdTokenProvider())
    {
    }

    public McpAgentToolProvider(IConfiguration configuration, IMcpIdTokenProvider idTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(idTokenProvider);

        var endpoint = configuration["Agent:McpEndpoint"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsedEndpoint) ||
            !parsedEndpoint.AbsolutePath.EndsWith("/mcp", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Agent MCP endpoint is missing or invalid. Configure Agent:McpEndpoint as a full URL ending in /mcp.");
        }

        _endpoint = parsedEndpoint;
        _idTokenProvider = idTokenProvider;
    }

    public async Task<IAgentToolSession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        var httpClient = new HttpClient(new McpCloudRunAuthenticationHandler(_endpoint, _idTokenProvider));
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = _endpoint,
            TransportMode = HttpTransportMode.StreamableHttp
        }, httpClient, ownsHttpClient: true);
        var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        return new McpAgentToolSession(client);
    }

    private sealed class McpAgentToolSession(McpClient client) : IAgentToolSession
    {
        private IReadOnlyList<McpClientTool>? _tools;

        public async Task<IReadOnlyList<AgentToolDefinition>> DiscoverToolsAsync(CancellationToken cancellationToken)
        {
            _tools = (await client.ListToolsAsync(cancellationToken: cancellationToken)).ToArray();
            return _tools.Select(tool => new AgentToolDefinition(
                tool.Name,
                tool.Description ?? string.Empty,
                tool.JsonSchema.Clone())).ToArray();
        }

        public async Task<AgentToolExecutionResult> InvokeAsync(
            AgentToolInvocation invocation,
            CancellationToken cancellationToken)
        {
            var tool = _tools?.SingleOrDefault(candidate => candidate.Name == invocation.ToolName)
                ?? throw new InvalidOperationException(
                    $"MCP tool '{invocation.ToolName}' was not discovered for this chat request.");
            var result = await tool.CallAsync(
                ToArguments(invocation.Arguments),
                cancellationToken: cancellationToken);

            return new AgentToolExecutionResult(
                JsonSerializer.Serialize(new { isError = result.IsError ?? false, content = result.Content }),
                result.IsError ?? false);
        }

        public ValueTask DisposeAsync() => client.DisposeAsync();

        private static Dictionary<string, object?> ToArguments(JsonElement arguments)
        {
            return arguments.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertValue(property.Value),
                StringComparer.Ordinal);
        }

        private static object? ConvertValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Object => value.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => ConvertValue(property.Value),
                    StringComparer.Ordinal),
                JsonValueKind.Array => value.EnumerateArray().Select(ConvertValue).ToArray(),
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => throw new ArgumentException("MCP tool arguments contain an unsupported JSON value.")
            };
        }
    }
}
