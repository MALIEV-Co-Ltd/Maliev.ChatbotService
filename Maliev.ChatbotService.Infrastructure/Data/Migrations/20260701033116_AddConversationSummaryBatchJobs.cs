using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.ChatbotService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationSummaryBatchJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationSummaryBatchJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummaryBatchJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationSummaryBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StructuredSummary = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TokenUsageJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummaryBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSummaryBatchItems_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationSummaryBatchItems_ConversationSummaryBatchJobs_~",
                        column: x => x.BatchJobId,
                        principalTable: "ConversationSummaryBatchJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationSummaryBatchItems_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchItems_BatchJobId",
                table: "ConversationSummaryBatchItems",
                column: "BatchJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchItems_SessionId",
                table: "ConversationSummaryBatchItems",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchItems_Status",
                table: "ConversationSummaryBatchItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchItems_UserProfileId",
                table: "ConversationSummaryBatchItems",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchJobs_BatchName",
                table: "ConversationSummaryBatchJobs",
                column: "BatchName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchJobs_CreatedAt",
                table: "ConversationSummaryBatchJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaryBatchJobs_Status",
                table: "ConversationSummaryBatchJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSummaryBatchItems");

            migrationBuilder.DropTable(
                name: "ConversationSummaryBatchJobs");
        }
    }
}
