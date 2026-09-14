using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AtlasSupply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeterministicDemoSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "suppliers",
                columns: new[] { "Id", "ContactEmail", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), "procurement@northwind-industrial.example", true, "Northwind Industrial Components" },
                    { new Guid("11111111-1111-1111-1111-111111111102"), "orders@harborsteel.example", true, "Harbor Steel Works" },
                    { new Guid("11111111-1111-1111-1111-111111111103"), "supply@silverline-packaging.example", true, "Silverline Packaging Co." },
                    { new Guid("11111111-1111-1111-1111-111111111104"), "support@apex-logistics.example", false, "Apex Logistics Partners" },
                    { new Guid("11111111-1111-1111-1111-111111111105"), "contact@legacy-circuitry.example", false, "Legacy Circuitry Ltd." }
                });

            migrationBuilder.InsertData(
                table: "incidents",
                columns: new[] { "Id", "ClosedAtUtc", "CreatedAtUtc", "Description", "PurchaseOrderId", "ResolvedAtUtc", "Status", "SupplierId", "Type" },
                values: new object[] { new Guid("33333333-3333-3333-3333-333333333306"), null, new DateTime(2026, 3, 10, 7, 45, 0, 0, DateTimeKind.Utc), "Supplier advised extended lead time on legacy controller components.", null, null, "Open", new Guid("11111111-1111-1111-1111-111111111105"), "Delay" });

            migrationBuilder.InsertData(
                table: "purchase_orders",
                columns: new[] { "Id", "ApprovedAtUtc", "CreatedAtUtc", "ReceivedAtUtc", "Status", "SubmittedAtUtc", "SupplierId" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222201"), new DateTime(2026, 1, 7, 9, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 6, 9, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 14, 9, 0, 0, 0, DateTimeKind.Utc), "Received", new DateTime(2026, 1, 6, 12, 0, 0, 0, DateTimeKind.Utc), new Guid("11111111-1111-1111-1111-111111111101") },
                    { new Guid("22222222-2222-2222-2222-222222222202"), null, new DateTime(2026, 2, 3, 9, 15, 0, 0, DateTimeKind.Utc), null, "Submitted", new DateTime(2026, 2, 3, 13, 15, 0, 0, DateTimeKind.Utc), new Guid("11111111-1111-1111-1111-111111111102") },
                    { new Guid("22222222-2222-2222-2222-222222222203"), new DateTime(2026, 2, 11, 8, 20, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 10, 8, 20, 0, 0, DateTimeKind.Utc), null, "Approved", new DateTime(2026, 2, 10, 10, 20, 0, 0, DateTimeKind.Utc), new Guid("11111111-1111-1111-1111-111111111103") },
                    { new Guid("22222222-2222-2222-2222-222222222204"), null, new DateTime(2026, 1, 20, 13, 0, 0, 0, DateTimeKind.Utc), null, "Cancelled", new DateTime(2026, 1, 20, 15, 0, 0, 0, DateTimeKind.Utc), new Guid("11111111-1111-1111-1111-111111111105") },
                    { new Guid("22222222-2222-2222-2222-222222222205"), null, new DateTime(2026, 3, 1, 8, 0, 0, 0, DateTimeKind.Utc), null, "Draft", null, new Guid("11111111-1111-1111-1111-111111111104") },
                    { new Guid("22222222-2222-2222-2222-222222222206"), new DateTime(2025, 12, 16, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 15, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 21, 10, 0, 0, 0, DateTimeKind.Utc), "Received", new DateTime(2025, 12, 15, 11, 0, 0, 0, DateTimeKind.Utc), new Guid("11111111-1111-1111-1111-111111111102") }
                });

            migrationBuilder.InsertData(
                table: "incidents",
                columns: new[] { "Id", "ClosedAtUtc", "CreatedAtUtc", "Description", "PurchaseOrderId", "ResolvedAtUtc", "Status", "SupplierId", "Type" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333301"), null, new DateTime(2026, 3, 5, 9, 30, 0, 0, DateTimeKind.Utc), "Carrier booking rollover pushed inbound cartons by four business days.", new Guid("22222222-2222-2222-2222-222222222203"), null, "InProgress", new Guid("11111111-1111-1111-1111-111111111103"), "Delay" },
                    { new Guid("33333333-3333-3333-3333-333333333302"), null, new DateTime(2026, 3, 8, 10, 0, 0, 0, DateTimeKind.Utc), "Incoming washer lot shows inconsistent zinc coating thickness.", new Guid("22222222-2222-2222-2222-222222222201"), null, "Open", new Guid("11111111-1111-1111-1111-111111111101"), "QualityIssue" },
                    { new Guid("33333333-3333-3333-3333-333333333303"), null, new DateTime(2026, 1, 13, 9, 0, 0, 0, DateTimeKind.Utc), "Initial steel plate lot arrived short by 12 units; backfill shipment confirmed.", new Guid("22222222-2222-2222-2222-222222222202"), new DateTime(2026, 1, 15, 9, 0, 0, 0, DateTimeKind.Utc), "Resolved", new Guid("11111111-1111-1111-1111-111111111102"), "ShortShipment" },
                    { new Guid("33333333-3333-3333-3333-333333333304"), new DateTime(2025, 12, 24, 8, 30, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 21, 8, 30, 0, 0, DateTimeKind.Utc), "Forklift puncture damaged five bearing cartons during receiving.", new Guid("22222222-2222-2222-2222-222222222206"), new DateTime(2025, 12, 22, 8, 30, 0, 0, DateTimeKind.Utc), "Closed", new Guid("11111111-1111-1111-1111-111111111102"), "DamagedGoods" },
                    { new Guid("33333333-3333-3333-3333-333333333305"), null, new DateTime(2026, 2, 2, 14, 0, 0, 0, DateTimeKind.Utc), "Duplicate service ticket created during shift handoff.", new Guid("22222222-2222-2222-2222-222222222205"), null, "Cancelled", new Guid("11111111-1111-1111-1111-111111111104"), "Other" }
                });

            migrationBuilder.InsertData(
                table: "purchase_order_items",
                columns: new[] { "Id", "Description", "PurchaseOrderId", "Quantity", "UnitPrice" },
                values: new object[,]
                {
                    { new Guid("44444444-4444-4444-4444-444444444401"), "M12 galvanized hex bolts", new Guid("22222222-2222-2222-2222-222222222201"), 500, 0.36m },
                    { new Guid("44444444-4444-4444-4444-444444444402"), "M12 lock washers", new Guid("22222222-2222-2222-2222-222222222201"), 500, 0.08m },
                    { new Guid("44444444-4444-4444-4444-444444444403"), "A36 steel plate 10mm", new Guid("22222222-2222-2222-2222-222222222202"), 80, 42.75m },
                    { new Guid("44444444-4444-4444-4444-444444444404"), "Structural angle L50x50x6", new Guid("22222222-2222-2222-2222-222222222202"), 120, 18.40m },
                    { new Guid("44444444-4444-4444-4444-444444444405"), "Double-wall shipping cartons", new Guid("22222222-2222-2222-2222-222222222203"), 1500, 1.22m },
                    { new Guid("44444444-4444-4444-4444-444444444406"), "Pallet stretch film 500mm", new Guid("22222222-2222-2222-2222-222222222203"), 200, 7.90m },
                    { new Guid("44444444-4444-4444-4444-444444444407"), "Control board rev C", new Guid("22222222-2222-2222-2222-222222222204"), 30, 185.00m },
                    { new Guid("44444444-4444-4444-4444-444444444408"), "Inbound dock scheduling service", new Guid("22222222-2222-2222-2222-222222222205"), 12, 320.00m },
                    { new Guid("44444444-4444-4444-4444-444444444409"), "Cross-dock handling support", new Guid("22222222-2222-2222-2222-222222222205"), 10, 280.00m },
                    { new Guid("44444444-4444-4444-4444-444444444410"), "Sealed roller bearings 6205", new Guid("22222222-2222-2222-2222-222222222206"), 240, 6.45m },
                    { new Guid("44444444-4444-4444-4444-444444444411"), "Bearing housing assemblies", new Guid("22222222-2222-2222-2222-222222222206"), 60, 24.30m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "incidents",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333301"));

            migrationBuilder.DeleteData(
                table: "incidents",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333302"));

            migrationBuilder.DeleteData(
                table: "incidents",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333303"));

            migrationBuilder.DeleteData(
                table: "incidents",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333304"));

            migrationBuilder.DeleteData(
                table: "incidents",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333305"));

            migrationBuilder.DeleteData(
                table: "incidents",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333306"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444401"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444402"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444403"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444404"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444405"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444406"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444407"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444408"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444409"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444410"));

            migrationBuilder.DeleteData(
                table: "purchase_order_items",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444411"));

            migrationBuilder.DeleteData(
                table: "purchase_orders",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222201"));

            migrationBuilder.DeleteData(
                table: "purchase_orders",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222202"));

            migrationBuilder.DeleteData(
                table: "purchase_orders",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222203"));

            migrationBuilder.DeleteData(
                table: "purchase_orders",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222204"));

            migrationBuilder.DeleteData(
                table: "purchase_orders",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222205"));

            migrationBuilder.DeleteData(
                table: "purchase_orders",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222206"));

            migrationBuilder.DeleteData(
                table: "suppliers",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"));

            migrationBuilder.DeleteData(
                table: "suppliers",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"));

            migrationBuilder.DeleteData(
                table: "suppliers",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"));

            migrationBuilder.DeleteData(
                table: "suppliers",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"));

            migrationBuilder.DeleteData(
                table: "suppliers",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111105"));
        }
    }
}
