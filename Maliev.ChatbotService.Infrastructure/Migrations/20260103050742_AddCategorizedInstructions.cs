using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ChatbotService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorizedInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "SystemInstructions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "SystemInstructions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TopicKey",
                table: "SystemInstructions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KnowledgeBase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FactKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBase", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "SystemInstructions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "Category", "TopicKey" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_SystemInstructions_TopicKey",
                table: "SystemInstructions",
                column: "TopicKey");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBase_TopicKey",
                table: "KnowledgeBase",
                column: "TopicKey");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBase_TopicKey_FactKey",
                table: "KnowledgeBase",
                columns: new[] { "TopicKey", "FactKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeBase");

            migrationBuilder.DropIndex(
                name: "IX_SystemInstructions_TopicKey",
                table: "SystemInstructions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "SystemInstructions");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "SystemInstructions");

            migrationBuilder.DropColumn(
                name: "TopicKey",
                table: "SystemInstructions");
        }
    }
}
