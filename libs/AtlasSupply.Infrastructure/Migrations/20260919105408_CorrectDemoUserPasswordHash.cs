using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtlasSupply.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrectDemoUserPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555501"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEC0xelScbohDD1Wy5JYjfcFhomon5G+mKyqcy80ksN2qwkulCTICD301wUF1xterYQ==");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555502"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEC0xelScbohDD1Wy5JYjfcFhomon5G+mKyqcy80ksN2qwkulCTICD301wUF1xterYQ==");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555501"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEAUxTC+MIKE77PFUkpiSQ/Ns35XdfbWERmzqQ+Je7SGrLj26Co3+L7kF1eCp4yjU6w==");

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555502"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEAUxTC+MIKE77PFUkpiSQ/Ns35XdfbWERmzqQ+Je7SGrLj26Co3+L7kF1eCp4yjU6w==");
        }
    }
}
