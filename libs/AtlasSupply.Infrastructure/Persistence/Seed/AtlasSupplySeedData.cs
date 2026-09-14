using System;

namespace AtlasSupply.Infrastructure.Persistence.Seed;

internal static class AtlasSupplySeedData
{
    internal static readonly Guid SupplierNorthwindIndustrialId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    internal static readonly Guid SupplierHarborSteelId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    internal static readonly Guid SupplierSilverlinePackagingId = Guid.Parse("11111111-1111-1111-1111-111111111103");
    internal static readonly Guid SupplierApexLogisticsId = Guid.Parse("11111111-1111-1111-1111-111111111104");
    internal static readonly Guid SupplierLegacyCircuitryId = Guid.Parse("11111111-1111-1111-1111-111111111105");

    internal static readonly Guid PurchaseOrderCompletedFastenersId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    internal static readonly Guid PurchaseOrderPendingApprovalId = Guid.Parse("22222222-2222-2222-2222-222222222202");
    internal static readonly Guid PurchaseOrderDelayedInboundId = Guid.Parse("22222222-2222-2222-2222-222222222203");
    internal static readonly Guid PurchaseOrderCancelledLegacyId = Guid.Parse("22222222-2222-2222-2222-222222222204");
    internal static readonly Guid PurchaseOrderDraftDockId = Guid.Parse("22222222-2222-2222-2222-222222222205");
    internal static readonly Guid PurchaseOrderCompletedBearingsId = Guid.Parse("22222222-2222-2222-2222-222222222206");

    internal static readonly Guid IncidentDelayInProgressId = Guid.Parse("33333333-3333-3333-3333-333333333301");
    internal static readonly Guid IncidentQualityOpenId = Guid.Parse("33333333-3333-3333-3333-333333333302");
    internal static readonly Guid IncidentShortShipmentResolvedId = Guid.Parse("33333333-3333-3333-3333-333333333303");
    internal static readonly Guid IncidentDamagedClosedId = Guid.Parse("33333333-3333-3333-3333-333333333304");
    internal static readonly Guid IncidentOtherCancelledId = Guid.Parse("33333333-3333-3333-3333-333333333305");
    internal static readonly Guid IncidentDelayOpenId = Guid.Parse("33333333-3333-3333-3333-333333333306");

    internal static readonly DateTime PurchaseOrderCompletedFastenersCreatedAtUtc = Utc(2026, 1, 6, 9, 0);
    internal static readonly DateTime PurchaseOrderPendingApprovalCreatedAtUtc = Utc(2026, 2, 3, 9, 15);
    internal static readonly DateTime PurchaseOrderDelayedInboundCreatedAtUtc = Utc(2026, 2, 10, 8, 20);
    internal static readonly DateTime PurchaseOrderCancelledLegacyCreatedAtUtc = Utc(2026, 1, 20, 13, 0);
    internal static readonly DateTime PurchaseOrderDraftDockCreatedAtUtc = Utc(2026, 3, 1, 8, 0);
    internal static readonly DateTime PurchaseOrderCompletedBearingsCreatedAtUtc = Utc(2025, 12, 15, 10, 0);

    internal static readonly DateTime IncidentDelayInProgressCreatedAtUtc = Utc(2026, 3, 5, 9, 30);
    internal static readonly DateTime IncidentQualityOpenCreatedAtUtc = Utc(2026, 3, 8, 10, 0);
    internal static readonly DateTime IncidentShortShipmentResolvedCreatedAtUtc = Utc(2026, 1, 13, 9, 0);
    internal static readonly DateTime IncidentDamagedClosedCreatedAtUtc = Utc(2025, 12, 21, 8, 30);
    internal static readonly DateTime IncidentOtherCancelledCreatedAtUtc = Utc(2026, 2, 2, 14, 0);
    internal static readonly DateTime IncidentDelayOpenCreatedAtUtc = Utc(2026, 3, 10, 7, 45);

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }
}
