using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.NormalizedUsername)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.NormalizedUsername)
            .IsUnique();

        builder.Property(x => x.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.HasMany(x => x.ScopeAssignments)
            .WithOne()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new
            {
                Id = AtlasSupplySeedData.ReadOnlyUserId,
                Username = "readonly",
                NormalizedUsername = "READONLY",
                Email = "readonly@atlas-supply.local",
                NormalizedEmail = "READONLY@ATLAS-SUPPLY.LOCAL",
                PasswordHash = AtlasSupplySeedData.DemoUserPasswordHash
            },
            new
            {
                Id = AtlasSupplySeedData.OperatorUserId,
                Username = "operator",
                NormalizedUsername = "OPERATOR",
                Email = "operator@atlas-supply.local",
                NormalizedEmail = "OPERATOR@ATLAS-SUPPLY.LOCAL",
                PasswordHash = AtlasSupplySeedData.DemoUserPasswordHash
            });
    }
}
