using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace AtlasSupply.Infrastructure.Persistence;

public sealed class AtlasSupplyDbContext(DbContextOptions<AtlasSupplyDbContext> options) : DbContext(options)
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<User> Users => Set<User>();

    internal DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    internal DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssemblyMarker).Assembly);
    }
}
