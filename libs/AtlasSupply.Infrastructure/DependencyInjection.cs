using AtlasSupply.Application;
using AtlasSupply.Infrastructure.Agents;
using AtlasSupply.Infrastructure.Knowledge;
using AtlasSupply.Infrastructure.Persistence;
using AtlasSupply.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AtlasSupply.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = ResolvePostgreSqlConnectionString(configuration);

        services.AddDbContext<AtlasSupplyDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(AssemblyMarker).Assembly.FullName)
                    .UseVector()));

        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddSingleton<IMarkdownKnowledgeChunker, MarkdownKnowledgeChunker>();
        services.AddSingleton<IEmbeddingService, OpenAIEmbeddingService>();
        services.AddScoped<IKnowledgeChunkSearch, KnowledgeChunkSearch>();
        services.AddScoped<IKnowledgeRetrievalService, SemanticKnowledgeRetrievalService>();
        services.AddScoped<IKnowledgeIngestionService, KnowledgeIngestionService>();
        services.AddScoped<IAgentLanguageModel, OpenAIAgentLanguageModel>();
        services.AddScoped<IAgentToolProvider, McpAgentToolProvider>();
        services.AddScoped<AgentService>(serviceProvider => new AgentService(
            serviceProvider.GetRequiredService<IAgentLanguageModel>(),
            serviceProvider.GetRequiredService<IAgentToolProvider>(),
            serviceProvider.GetRequiredService<IKnowledgeRetrievalService>(),
            new AgentServiceOptions(ParseMaximumToolRounds(configuration))));
        services.Configure<KnowledgeIngestionOptions>(options =>
            options.SourceDirectory = configuration["Knowledge:SourceDirectory"] ?? "docs/knowledge");

        return services;
    }

    private static string ResolvePostgreSqlConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? configuration["POSTGRESQL_CONNECTION_STRING"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        throw new InvalidOperationException(
            "PostgreSQL connection string is missing. Configure ConnectionStrings:PostgreSQL or POSTGRESQL_CONNECTION_STRING.");
    }

    private static int ParseMaximumToolRounds(IConfiguration configuration)
    {
        var configuredValue = configuration["Agent:MaximumToolRounds"];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return 4;
        }

        if (!int.TryParse(configuredValue, out var maximumToolRounds) || maximumToolRounds is < 1 or > 8)
        {
            throw new InvalidOperationException(
                "Agent:MaximumToolRounds must be an integer between 1 and 8.");
        }

        return maximumToolRounds;
    }
}
