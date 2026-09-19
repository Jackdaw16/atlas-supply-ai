using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasSupply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentToolAuditRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_tool_audit_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredScope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Authorized = table.Column<bool>(type: "boolean", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_tool_audit_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_tool_audit_records_Authorized",
                table: "agent_tool_audit_records",
                column: "Authorized");

            migrationBuilder.CreateIndex(
                name: "IX_agent_tool_audit_records_TimestampUtc",
                table: "agent_tool_audit_records",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_agent_tool_audit_records_ToolName",
                table: "agent_tool_audit_records",
                column: "ToolName");

            migrationBuilder.CreateIndex(
                name: "IX_agent_tool_audit_records_UserId",
                table: "agent_tool_audit_records",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_tool_audit_records");
        }
    }
}
