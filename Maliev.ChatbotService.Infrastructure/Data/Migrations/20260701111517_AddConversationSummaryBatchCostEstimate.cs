using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ChatbotService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationSummaryBatchCostEstimate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostEstimateJson",
                table: "ConversationSummaryBatchItems",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostEstimateJson",
                table: "ConversationSummaryBatchItems");
        }
    }
}
