using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("purchase_order_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property<Guid>("PurchaseOrderId")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Ignore(x => x.LineTotal);

        builder.HasData(
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444401"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderCompletedFastenersId,
                Description = "M12 galvanized hex bolts",
                Quantity = 500,
                UnitPrice = 0.36m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444402"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderCompletedFastenersId,
                Description = "M12 lock washers",
                Quantity = 500,
                UnitPrice = 0.08m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444403"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderPendingApprovalId,
                Description = "A36 steel plate 10mm",
                Quantity = 80,
                UnitPrice = 42.75m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444404"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderPendingApprovalId,
                Description = "Structural angle L50x50x6",
                Quantity = 120,
                UnitPrice = 18.40m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444405"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderDelayedInboundId,
                Description = "Double-wall shipping cartons",
                Quantity = 1500,
                UnitPrice = 1.22m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444406"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderDelayedInboundId,
                Description = "Pallet stretch film 500mm",
                Quantity = 200,
                UnitPrice = 7.90m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444407"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderCancelledLegacyId,
                Description = "Control board rev C",
                Quantity = 30,
                UnitPrice = 185.00m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444408"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderDraftDockId,
                Description = "Inbound dock scheduling service",
                Quantity = 12,
                UnitPrice = 320.00m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444409"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderDraftDockId,
                Description = "Cross-dock handling support",
                Quantity = 10,
                UnitPrice = 280.00m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444410"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderCompletedBearingsId,
                Description = "Sealed roller bearings 6205",
                Quantity = 240,
                UnitPrice = 6.45m
            },
            new
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444411"),
                PurchaseOrderId = AtlasSupplySeedData.PurchaseOrderCompletedBearingsId,
                Description = "Bearing housing assemblies",
                Quantity = 60,
                UnitPrice = 24.30m
            });
    }
}
