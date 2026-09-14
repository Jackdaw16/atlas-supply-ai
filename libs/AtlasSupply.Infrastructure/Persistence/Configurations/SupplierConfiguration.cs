using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ContactEmail)
            .HasMaxLength(320);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasData(
            new
            {
                Id = AtlasSupplySeedData.SupplierNorthwindIndustrialId,
                Name = "Northwind Industrial Components",
                ContactEmail = "procurement@northwind-industrial.example",
                IsActive = true
            },
            new
            {
                Id = AtlasSupplySeedData.SupplierHarborSteelId,
                Name = "Harbor Steel Works",
                ContactEmail = "orders@harborsteel.example",
                IsActive = true
            },
            new
            {
                Id = AtlasSupplySeedData.SupplierSilverlinePackagingId,
                Name = "Silverline Packaging Co.",
                ContactEmail = "supply@silverline-packaging.example",
                IsActive = true
            },
            new
            {
                Id = AtlasSupplySeedData.SupplierApexLogisticsId,
                Name = "Apex Logistics Partners",
                ContactEmail = "support@apex-logistics.example",
                IsActive = false
            },
            new
            {
                Id = AtlasSupplySeedData.SupplierLegacyCircuitryId,
                Name = "Legacy Circuitry Ltd.",
                ContactEmail = "contact@legacy-circuitry.example",
                IsActive = false
            });
    }
}
