using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ChatbotService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebSearchFieldsToSystemInstruction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableWebSearch",
                table: "SystemInstructions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LogSearchDomains",
                table: "SystemInstructions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "SystemInstructions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "EnableWebSearch", "LogSearchDomains" },
                values: new object[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableWebSearch",
                table: "SystemInstructions");

            migrationBuilder.DropColumn(
                name: "LogSearchDomains",
                table: "SystemInstructions");
        }
    }
}
