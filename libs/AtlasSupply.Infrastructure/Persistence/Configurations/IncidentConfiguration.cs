using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AtlasSupply.Infrastructure.Persistence.Configurations;

internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SupplierId)
            .IsRequired();

        builder.Property(x => x.PurchaseOrderId);

        builder.Property(x => x.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ResolvedAtUtc);

        builder.Property(x => x.ClosedAtUtc);

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PurchaseOrder>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasData(
            new
            {
                Id = AtlasSupplySeedData.IncidentDelayInProgressId,
                Type = IncidentType.Delay,
                Status = IncidentStatus.InProgress,
                SupplierId = AtlasSupplySeedData.SupplierSilverlinePackagingId,
                PurchaseOrderId = (Guid?)AtlasSupplySeedData.PurchaseOrderDelayedInboundId,
                Description = "Carrier booking rollover pushed inbound cartons by four business days.",
                CreatedAtUtc = AtlasSupplySeedData.IncidentDelayInProgressCreatedAtUtc,
                ResolvedAtUtc = (DateTime?)null,
                ClosedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.IncidentQualityOpenId,
                Type = IncidentType.QualityIssue,
                Status = IncidentStatus.Open,
                SupplierId = AtlasSupplySeedData.SupplierNorthwindIndustrialId,
                PurchaseOrderId = (Guid?)AtlasSupplySeedData.PurchaseOrderCompletedFastenersId,
                Description = "Incoming washer lot shows inconsistent zinc coating thickness.",
                CreatedAtUtc = AtlasSupplySeedData.IncidentQualityOpenCreatedAtUtc,
                ResolvedAtUtc = (DateTime?)null,
                ClosedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.IncidentShortShipmentResolvedId,
                Type = IncidentType.ShortShipment,
                Status = IncidentStatus.Resolved,
                SupplierId = AtlasSupplySeedData.SupplierHarborSteelId,
                PurchaseOrderId = (Guid?)AtlasSupplySeedData.PurchaseOrderPendingApprovalId,
                Description = "Initial steel plate lot arrived short by 12 units; backfill shipment confirmed.",
                CreatedAtUtc = AtlasSupplySeedData.IncidentShortShipmentResolvedCreatedAtUtc,
                ResolvedAtUtc = AtlasSupplySeedData.IncidentShortShipmentResolvedCreatedAtUtc.AddDays(2),
                ClosedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.IncidentDamagedClosedId,
                Type = IncidentType.DamagedGoods,
                Status = IncidentStatus.Closed,
                SupplierId = AtlasSupplySeedData.SupplierHarborSteelId,
                PurchaseOrderId = (Guid?)AtlasSupplySeedData.PurchaseOrderCompletedBearingsId,
                Description = "Forklift puncture damaged five bearing cartons during receiving.",
                CreatedAtUtc = AtlasSupplySeedData.IncidentDamagedClosedCreatedAtUtc,
                ResolvedAtUtc = AtlasSupplySeedData.IncidentDamagedClosedCreatedAtUtc.AddDays(1),
                ClosedAtUtc = AtlasSupplySeedData.IncidentDamagedClosedCreatedAtUtc.AddDays(3)
            },
            new
            {
                Id = AtlasSupplySeedData.IncidentOtherCancelledId,
                Type = IncidentType.Other,
                Status = IncidentStatus.Cancelled,
                SupplierId = AtlasSupplySeedData.SupplierApexLogisticsId,
                PurchaseOrderId = (Guid?)AtlasSupplySeedData.PurchaseOrderDraftDockId,
                Description = "Duplicate service ticket created during shift handoff.",
                CreatedAtUtc = AtlasSupplySeedData.IncidentOtherCancelledCreatedAtUtc,
                ResolvedAtUtc = (DateTime?)null,
                ClosedAtUtc = (DateTime?)null
            },
            new
            {
                Id = AtlasSupplySeedData.IncidentDelayOpenId,
                Type = IncidentType.Delay,
                Status = IncidentStatus.Open,
                SupplierId = AtlasSupplySeedData.SupplierLegacyCircuitryId,
                PurchaseOrderId = (Guid?)null,
                Description = "Supplier advised extended lead time on legacy controller components.",
                CreatedAtUtc = AtlasSupplySeedData.IncidentDelayOpenCreatedAtUtc,
                ResolvedAtUtc = (DateTime?)null,
                ClosedAtUtc = (DateTime?)null
            });
    }
}
