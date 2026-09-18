using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AtlasSupply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScopeAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_scope_assignments",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_scope_assignments", x => new { x.UserId, x.Scope });
                    table.CheckConstraint("CK_user_scope_assignments_Scope", "\"Scope\" IN ('suppliers.list', 'suppliers.read', 'orders.delayed.read', 'incidents.create', 'knowledge.search')");
                    table.ForeignKey(
                        name: "FK_user_scope_assignments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "Email", "NormalizedEmail", "NormalizedUsername", "PasswordHash", "Username" },
                values: new object[,]
                {
                    { new Guid("55555555-5555-5555-5555-555555555501"), "readonly@atlas-supply.local", "READONLY@ATLAS-SUPPLY.LOCAL", "READONLY", "AQAAAAIAAYagAAAAEAUxTC+MIKE77PFUkpiSQ/Ns35XdfbWERmzqQ+Je7SGrLj26Co3+L7kF1eCp4yjU6w==", "readonly" },
                    { new Guid("55555555-5555-5555-5555-555555555502"), "operator@atlas-supply.local", "OPERATOR@ATLAS-SUPPLY.LOCAL", "OPERATOR", "AQAAAAIAAYagAAAAEAUxTC+MIKE77PFUkpiSQ/Ns35XdfbWERmzqQ+Je7SGrLj26Co3+L7kF1eCp4yjU6w==", "operator" }
                });

            migrationBuilder.InsertData(
                table: "user_scope_assignments",
                columns: new[] { "Scope", "UserId" },
                values: new object[,]
                {
                    { "knowledge.search", new Guid("55555555-5555-5555-5555-555555555501") },
                    { "orders.delayed.read", new Guid("55555555-5555-5555-5555-555555555501") },
                    { "suppliers.list", new Guid("55555555-5555-5555-5555-555555555501") },
                    { "suppliers.read", new Guid("55555555-5555-5555-5555-555555555501") },
                    { "incidents.create", new Guid("55555555-5555-5555-5555-555555555502") },
                    { "knowledge.search", new Guid("55555555-5555-5555-5555-555555555502") },
                    { "orders.delayed.read", new Guid("55555555-5555-5555-5555-555555555502") },
                    { "suppliers.list", new Guid("55555555-5555-5555-5555-555555555502") },
                    { "suppliers.read", new Guid("55555555-5555-5555-5555-555555555502") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedEmail",
                table: "users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedUsername",
                table: "users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_scope_assignments");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
