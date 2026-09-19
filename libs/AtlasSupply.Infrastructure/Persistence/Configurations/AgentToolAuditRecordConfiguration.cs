using AtlasSupply.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class AgentToolAuditRecordConfiguration : IEntityTypeConfiguration<AgentToolAuditRecord>
{
    public void Configure(EntityTypeBuilder<AgentToolAuditRecord> builder)
    {
        builder.ToTable("agent_tool_audit_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ToolName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RequiredScope)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Authorized)
            .IsRequired();

        builder.Property(x => x.Succeeded)
            .IsRequired();

        builder.Property(x => x.TimestampUtc)
            .IsRequired();

        builder.Property(x => x.Outcome)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => x.TimestampUtc);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ToolName);
        builder.HasIndex(x => x.Authorized);
    }
}
