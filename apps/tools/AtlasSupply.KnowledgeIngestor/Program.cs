using AtlasSupply.Application;
using AtlasSupply.Infrastructure;
using AtlasSupply.KnowledgeIngestor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var sourceDirectory = KnowledgeIngestionCommand.ParseSourceDirectory(args);
var builder = Host.CreateApplicationBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["POSTGRESQL_CONNECTION_STRING"]))
{
    await Console.Error.WriteLineAsync("POSTGRESQL_CONNECTION_STRING is required for knowledge ingestion.");
    return 1;
}

if (string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_API_KEY"]))
{
    await Console.Error.WriteLineAsync("OPENAI_API_KEY is required for knowledge ingestion.");
    return 1;
}

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Knowledge:SourceDirectory"] = sourceDirectory,
    ["Agent:McpEndpoint"] = builder.Configuration["Agent:McpEndpoint"] ?? "http://localhost:5001/mcp"
});
builder.Services.AddInfrastructure(builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var command = new KnowledgeIngestionCommand(
    scope.ServiceProvider.GetRequiredService<IKnowledgeIngestionService>(),
    Console.Out,
    Console.Error);

return await command.ExecuteAsync(sourceDirectory, CancellationToken.None);
