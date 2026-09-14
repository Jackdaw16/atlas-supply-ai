using AtlasSupply.Application;
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
                npgsqlOptions.MigrationsAssembly(typeof(AssemblyMarker).Assembly.FullName)));

        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();

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
}
