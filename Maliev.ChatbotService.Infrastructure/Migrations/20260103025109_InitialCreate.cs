using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Maliev.ChatbotService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FallbackResponseTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    ResponseText = table.Column<string>(type: "text", nullable: false),
                    IncludesContactInfo = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FallbackResponseTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemInstructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PersonaDefinition = table.Column<string>(type: "text", nullable: false),
                    BusinessConstraints = table.Column<string>(type: "text", nullable: false),
                    AllowedTopics = table.Column<string>(type: "text", nullable: false),
                    RejectionTemplates = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EnableWebSearch = table.Column<bool>(type: "boolean", nullable: false),
                    LogSearchDomains = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemInstructions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InternalUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LineUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FacebookId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InstagramId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WhatsAppId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SummaryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSessions_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformName = table.Column<int>(type: "integer", nullable: false),
                    ExternalPlatformId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WebhookConfirmationStatus = table.Column<int>(type: "integer", nullable: false),
                    LinkCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdentityLinks_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructuredSummary = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSummaries_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationSummaries_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SearchDomainLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SearchQuery = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchDomainLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchDomainLogs_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperationParameters = table.Column<string>(type: "jsonb", nullable: true),
                    ExecutionResult = table.Column<string>(type: "jsonb", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationLogs_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OperationLogs_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "jsonb", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMemories_Messages_SourceMessageId",
                        column: x => x.SourceMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserMemories_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "FallbackResponseTemplates",
                columns: new[] { "Id", "CreatedAt", "IncludesContactInfo", "IsActive", "Language", "Priority", "ResponseText", "ScenarioType", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000010"), new DateTimeOffset(new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, true, 1, 100, "I'm Mali, and I apologize, but I encountered an unexpected error while processing your request. Please try again in a few moments. If the issue persists, you can contact our support team at support@maliev.com.", "UnexpectedError", null },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new DateTimeOffset(new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, true, 2, 100, "?????????????????? ??????????????????????????????????????????????? ??????????????????????????? ????????????????? ?????????????????????????????????????? support@maliev.com ???", "UnexpectedError", null }
                });

            migrationBuilder.InsertData(
                table: "SystemInstructions",
                columns: new[] { "Id", "AllowedTopics", "BusinessConstraints", "EnableWebSearch", "IsActive", "LogSearchDomains", "Name", "PersonaDefinition", "RejectionTemplates", "Version" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "manufacturing,materials,processes,orders,quotations,technical specifications,quality standards,production capabilities,lead times,CNC machining,welding,casting,forging,sheet metal fabrication,metals,plastics,composites,ISO standards,ASTM standards,DIN standards", "STRICT RULES - YOU MUST FOLLOW THESE:\n\n1. ONLY discuss topics related to manufacturing, materials, processes, orders, and Maliev company services\n2. REJECT politely any questions about:\n   - Weather, sports, entertainment, politics\n   - Personal advice\n   - Competitor services or pricing\n   - Non-manufacturing topics\n\n3. When rejecting off-topic questions, ALWAYS redirect to manufacturing topics like:\n   \"I'm Mali, and I'm specialized in manufacturing assistance. I can help you with:\n   - Material selection and specifications\n   - Manufacturing process recommendations\n   - Order status and quotations\n   - Technical drawings and requirements\n   What would you like to know about our manufacturing services?\"\n\n4. For internal agents with CRM access:\n   - Provide quotation and order status details\n   - Offer quick action buttons (Send Reminder, Update Status, etc.)\n   - Access customer history\n\n5. NEVER:\n   - Share confidential company information\n   - Make commitments without proper authorization\n   - Provide competitor pricing or comparison", false, true, true, "Manufacturing Chatbot - Default", "You are Mali, a helpful and knowledgeable female AI assistant for Maliev Manufacturing Company. You specialize in manufacturing processes, materials, and customer inquiries about our services.\n\nYour expertise includes:\n- Manufacturing processes (CNC machining, welding, casting, forging, sheet metal fabrication)\n- Materials (metals, plastics, composites)\n- Quality standards (ISO, ASTM, DIN)\n- Production capabilities and lead times\n- Technical specifications and drawings\n- Order status and quotation information\n\nCommunication style:\n- Professional, warm, and courteous\n- Clear and concise\n- Technical when needed, but accessible to non-experts\n- Proactive in offering relevant information\n- Patient and supportive with follow-up questions", "{\n  \"weather\": \"?????????????? ?????????????????????????????? ???????????????????????????????? ?????????????????????????? ????????????? ????????????????????????????????? ?????????????????????????????????????????????????????????????\",\n  \"competitor\": \"??????????????????????????????????????????????????????????????? Maliev ??? ???????????????????????????????? ???? ??????????????????????????????????????????????????????? ???????????????????????????????\",\n  \"general\": \"?????????????????????????????????????????????????? ????????????????????????:\\n- ????????????????????????????????????\\n- ????????????????????????\\n- ????????????????????????????\\n- ???????????????????????????????\\n?????????????????????????????????????????????????????????????\"\n}", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_ExpiresAt",
                table: "ConversationSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_Status",
                table: "ConversationSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_UserProfileId",
                table: "ConversationSessions",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_UserProfileId_Channel_Status",
                table: "ConversationSessions",
                columns: new[] { "UserProfileId", "Channel", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_SessionId",
                table: "ConversationSummaries",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSummaries_UserProfileId",
                table: "ConversationSummaries",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackResponseTemplates_Priority",
                table: "FallbackResponseTemplates",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackResponseTemplates_ScenarioType_Language_IsActive",
                table: "FallbackResponseTemplates",
                columns: new[] { "ScenarioType", "Language", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityLinks_ExternalPlatformId",
                table: "IdentityLinks",
                column: "ExternalPlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityLinks_PlatformName_ExternalPlatformId",
                table: "IdentityLinks",
                columns: new[] { "PlatformName", "ExternalPlatformId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityLinks_UserProfileId",
                table: "IdentityLinks",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SessionId",
                table: "Messages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SessionId_CreatedAt",
                table: "Messages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_ExecutedAt",
                table: "OperationLogs",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_MessageId",
                table: "OperationLogs",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_UserProfileId",
                table: "OperationLogs",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_UserProfileId_ExecutedAt",
                table: "OperationLogs",
                columns: new[] { "UserProfileId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchDomainLogs_AccessedAt",
                table: "SearchDomainLogs",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchDomainLogs_Domain",
                table: "SearchDomainLogs",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_SearchDomainLogs_SessionId",
                table: "SearchDomainLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemInstructions_IsActive",
                table: "SystemInstructions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SystemInstructions_Name_Version",
                table: "SystemInstructions",
                columns: new[] { "Name", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_Confidence",
                table: "UserMemories",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_SourceMessageId",
                table: "UserMemories",
                column: "SourceMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserProfileId",
                table: "UserMemories",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserProfileId_Key",
                table: "UserMemories",
                columns: new[] { "UserProfileId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_FacebookId",
                table: "UserProfiles",
                column: "FacebookId",
                unique: true,
                filter: "\"FacebookId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_InstagramId",
                table: "UserProfiles",
                column: "InstagramId",
                unique: true,
                filter: "\"InstagramId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_InternalUserId",
                table: "UserProfiles",
                column: "InternalUserId",
                filter: "\"InternalUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_LineUserId",
                table: "UserProfiles",
                column: "LineUserId",
                unique: true,
                filter: "\"LineUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_WhatsAppId",
                table: "UserProfiles",
                column: "WhatsAppId",
                unique: true,
                filter: "\"WhatsAppId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSummaries");

            migrationBuilder.DropTable(
                name: "FallbackResponseTemplates");

            migrationBuilder.DropTable(
                name: "IdentityLinks");

            migrationBuilder.DropTable(
                name: "OperationLogs");

            migrationBuilder.DropTable(
                name: "SearchDomainLogs");

            migrationBuilder.DropTable(
                name: "SystemInstructions");

            migrationBuilder.DropTable(
                name: "UserMemories");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "ConversationSessions");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
