using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class UserScopeAssignmentConfiguration : IEntityTypeConfiguration<UserScopeAssignment>
{
    public void Configure(EntityTypeBuilder<UserScopeAssignment> builder)
    {
        builder.ToTable("user_scope_assignments", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_user_scope_assignments_Scope",
                "\"Scope\" IN ('suppliers.list', 'suppliers.read', 'orders.delayed.read', 'incidents.create', 'knowledge.search')"));

        builder.HasKey(x => new { x.UserId, x.Scope });

        builder.Property(x => x.Scope)
            .HasConversion(scope => scope.Value, value => AgentCapabilityScope.FromValue(value))
            .HasMaxLength(64)
            .IsRequired();

        builder.HasData(
            new { UserId = AtlasSupplySeedData.ReadOnlyUserId, Scope = AgentCapabilityScope.SuppliersList },
            new { UserId = AtlasSupplySeedData.ReadOnlyUserId, Scope = AgentCapabilityScope.SuppliersRead },
            new { UserId = AtlasSupplySeedData.ReadOnlyUserId, Scope = AgentCapabilityScope.OrdersDelayedRead },
            new { UserId = AtlasSupplySeedData.ReadOnlyUserId, Scope = AgentCapabilityScope.KnowledgeSearch },
            new { UserId = AtlasSupplySeedData.OperatorUserId, Scope = AgentCapabilityScope.SuppliersList },
            new { UserId = AtlasSupplySeedData.OperatorUserId, Scope = AgentCapabilityScope.SuppliersRead },
            new { UserId = AtlasSupplySeedData.OperatorUserId, Scope = AgentCapabilityScope.OrdersDelayedRead },
            new { UserId = AtlasSupplySeedData.OperatorUserId, Scope = AgentCapabilityScope.IncidentsCreate },
            new { UserId = AtlasSupplySeedData.OperatorUserId, Scope = AgentCapabilityScope.KnowledgeSearch });
    }
}
