using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.SupplierId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.SubmittedAtUtc);

        builder.Property(x => x.ApprovedAtUtc);

        builder.Property(x => x.ReceivedAtUtc);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey("PurchaseOrderId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.TotalAmount);

        builder.HasData(
            new
            {
                Id = AtlasSupplySeedData.PurchaseOrderCompletedFastenersId,
                SupplierId = AtlasSupplySeedData.SupplierNorthwindIndustrialId,
                Status = PurchaseOrderStatus.Received,
                CreatedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedFastenersCreatedAtUtc,
                SubmittedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedFastenersCreatedAtUtc.AddHours(3),
                ApprovedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedFastenersCreatedAtUtc.AddDays(1),
                ReceivedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedFastenersCreatedAtUtc.AddDays(8)
            },
            new
            {
                Id = AtlasSupplySeedData.PurchaseOrderPendingApprovalId,
                SupplierId = AtlasSupplySeedData.SupplierHarborSteelId,
                Status = PurchaseOrderStatus.Submitted,
                CreatedAtUtc = AtlasSupplySeedData.PurchaseOrderPendingApprovalCreatedAtUtc,
                SubmittedAtUtc = AtlasSupplySeedData.PurchaseOrderPendingApprovalCreatedAtUtc.AddHours(4),
                ApprovedAtUtc = (DateTime?)null,
                ReceivedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.PurchaseOrderDelayedInboundId,
                SupplierId = AtlasSupplySeedData.SupplierSilverlinePackagingId,
                Status = PurchaseOrderStatus.Approved,
                CreatedAtUtc = AtlasSupplySeedData.PurchaseOrderDelayedInboundCreatedAtUtc,
                SubmittedAtUtc = AtlasSupplySeedData.PurchaseOrderDelayedInboundCreatedAtUtc.AddHours(2),
                ApprovedAtUtc = AtlasSupplySeedData.PurchaseOrderDelayedInboundCreatedAtUtc.AddDays(1),
                ReceivedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.PurchaseOrderCancelledLegacyId,
                SupplierId = AtlasSupplySeedData.SupplierLegacyCircuitryId,
                Status = PurchaseOrderStatus.Cancelled,
                CreatedAtUtc = AtlasSupplySeedData.PurchaseOrderCancelledLegacyCreatedAtUtc,
                SubmittedAtUtc = AtlasSupplySeedData.PurchaseOrderCancelledLegacyCreatedAtUtc.AddHours(2),
                ApprovedAtUtc = (DateTime?)null,
                ReceivedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.PurchaseOrderDraftDockId,
                SupplierId = AtlasSupplySeedData.SupplierApexLogisticsId,
                Status = PurchaseOrderStatus.Draft,
                CreatedAtUtc = AtlasSupplySeedData.PurchaseOrderDraftDockCreatedAtUtc,
                SubmittedAtUtc = (DateTime?)null,
                ApprovedAtUtc = (DateTime?)null,
                ReceivedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.PurchaseOrderCompletedBearingsId,
                SupplierId = AtlasSupplySeedData.SupplierHarborSteelId,
                Status = PurchaseOrderStatus.Received,
                CreatedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedBearingsCreatedAtUtc,
                SubmittedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedBearingsCreatedAtUtc.AddHours(1),
                ApprovedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedBearingsCreatedAtUtc.AddDays(1),
                ReceivedAtUtc = AtlasSupplySeedData.PurchaseOrderCompletedBearingsCreatedAtUtc.AddDays(6)
            });
    }
}
