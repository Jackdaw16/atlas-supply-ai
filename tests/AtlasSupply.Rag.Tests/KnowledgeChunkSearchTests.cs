using AtlasSupply.Application;
using AtlasSupply.Infrastructure.Knowledge;
using AtlasSupply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace AtlasSupply.Rag.Tests;

public sealed class KnowledgeChunkSearchTests
{
    [Fact]
    public void CreateOrderedSearchQuery_TranslatesL2OrderingToSql()
    {
        var options = new DbContextOptionsBuilder<AtlasSupplyDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=atlas_supply;Username=atlas_supply;Password=not-used",
                npgsqlOptions => npgsqlOptions.UseVector())
            .Options;
        using var dbContext = new AtlasSupplyDbContext(options);

        var query = new KnowledgeChunkSearch(dbContext)
            .CreateOrderedSearchQuery(new Vector(new float[1_536]), 5);
        var sql = query.ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        Assert.Contains("<->", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(KnowledgeSearchResult), query.Expression.ToString(), StringComparison.Ordinal);
    }
}
