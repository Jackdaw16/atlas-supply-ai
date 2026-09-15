using AtlasSupply.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.DocumentId)
            .IsRequired();

        builder.Property(x => x.Ordinal)
            .IsRequired();

        builder.HasIndex(x => new { x.DocumentId, x.Ordinal })
            .IsUnique();

        builder.Property(x => x.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Heading)
            .HasMaxLength(256);

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1536)")
            .IsRequired();

        builder.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_l2_ops");

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();
    }
}
