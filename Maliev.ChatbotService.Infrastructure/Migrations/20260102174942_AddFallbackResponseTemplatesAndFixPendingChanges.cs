using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Maliev.ChatbotService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFallbackResponseTemplatesAndFixPendingChanges : Migration
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

            migrationBuilder.InsertData(
                table: "FallbackResponseTemplates",
                columns: new[] { "Id", "CreatedAt", "IncludesContactInfo", "IsActive", "Language", "Priority", "ResponseText", "ScenarioType", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000010"), new DateTimeOffset(new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, true, 1, 100, "I apologize, but I encountered an unexpected error while processing your request. Please try again in a few moments. If the issue persists, you can contact our support team at support@maliev.com.", "UnexpectedError", null },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new DateTimeOffset(new DateTime(2025, 12, 31, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, true, 2, 100, "ขออภัยด้วยครับ เกิดข้อผิดพลาดที่ไม่คาดคิดขณะประมวลผลคำขอของคุณ โปรดลองอีกครั้งในอีกสักครู่ หากปัญหายังคงอยู่ คุณสามารถติดต่อทีมสนับสนุนของเราได้ที่ support@maliev.com", "UnexpectedError", null }
                });

            migrationBuilder.UpdateData(
                table: "SystemInstructions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "BusinessConstraints", "PersonaDefinition", "RejectionTemplates" },
                values: new object[] { "STRICT RULES - YOU MUST FOLLOW THESE:\r\n\r\n1. ONLY discuss topics related to manufacturing, materials, processes, orders, and Maliev company services\r\n2. REJECT politely any questions about:\r\n   - Weather, sports, entertainment, politics\r\n   - Personal advice\r\n   - Competitor services or pricing\r\n   - Non-manufacturing topics\r\n\r\n3. When rejecting off-topic questions, ALWAYS redirect to manufacturing topics like:\r\n   \"I'm specialized in manufacturing assistance. I can help you with:\r\n   - Material selection and specifications\r\n   - Manufacturing process recommendations\r\n   - Order status and quotations\r\n   - Technical drawings and requirements\r\n   What would you like to know about our manufacturing services?\"\r\n\r\n4. For internal agents with CRM access:\r\n   - Provide quotation and order status details\r\n   - Offer quick action buttons (Send Reminder, Update Status, etc.)\r\n   - Access customer history\r\n\r\n5. NEVER:\r\n   - Share confidential company information\r\n   - Make commitments without proper authorization\r\n   - Provide competitor pricing or comparison", "You are a helpful AI assistant for Maliev Manufacturing Company. You specialize in manufacturing processes, materials, and customer inquiries about our services.\r\n\r\nYour expertise includes:\r\n- Manufacturing processes (CNC machining, welding, casting, forging, sheet metal fabrication)\r\n- Materials (metals, plastics, composites)\r\n- Quality standards (ISO, ASTM, DIN)\r\n- Production capabilities and lead times\r\n- Technical specifications and drawings\r\n- Order status and quotation information\r\n\r\nCommunication style:\r\n- Professional and courteous\r\n- Clear and concise\r\n- Technical when needed, but accessible to non-experts\r\n- Proactive in offering relevant information\r\n- Patient with follow-up questions", "{\r\n  \"weather\": \"I'm specialized in manufacturing assistance and don't have information about weather. However, I can help you with material selection, manufacturing processes, or order status. What would you like to know about our manufacturing services?\",\r\n  \"competitor\": \"I focus on helping you with Maliev's manufacturing services and capabilities. I'd be happy to discuss our offerings, pricing, or how we can meet your manufacturing needs. What specific requirements do you have?\",\r\n  \"general\": \"I'm here to assist with manufacturing-related questions. I can help you with:\\n- Material selection and specifications\\n- Manufacturing process recommendations\\n- Order status and quotations\\n- Technical drawings and requirements\\nWhat would you like to know about our manufacturing services?\"\r\n}" });

            migrationBuilder.CreateIndex(
                name: "IX_FallbackResponseTemplates_Priority",
                table: "FallbackResponseTemplates",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_FallbackResponseTemplates_ScenarioType_Language_IsActive",
                table: "FallbackResponseTemplates",
                columns: new[] { "ScenarioType", "Language", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FallbackResponseTemplates");

            migrationBuilder.UpdateData(
                table: "SystemInstructions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "BusinessConstraints", "PersonaDefinition", "RejectionTemplates" },
                values: new object[] { "STRICT RULES - YOU MUST FOLLOW THESE:\n\n1. ONLY discuss topics related to manufacturing, materials, processes, orders, and Maliev company services\n2. REJECT politely any questions about:\n   - Weather, sports, entertainment, politics\n   - Personal advice\n   - Competitor services or pricing\n   - Non-manufacturing topics\n\n3. When rejecting off-topic questions, ALWAYS redirect to manufacturing topics like:\n   \"I'm specialized in manufacturing assistance. I can help you with:\n   - Material selection and specifications\n   - Manufacturing process recommendations\n   - Order status and quotations\n   - Technical drawings and requirements\n   What would you like to know about our manufacturing services?\"\n\n4. For internal agents with CRM access:\n   - Provide quotation and order status details\n   - Offer quick action buttons (Send Reminder, Update Status, etc.)\n   - Access customer history\n\n5. NEVER:\n   - Share confidential company information\n   - Make commitments without proper authorization\n   - Provide competitor pricing or comparison", "You are a helpful AI assistant for Maliev Manufacturing Company. You specialize in manufacturing processes, materials, and customer inquiries about our services.\n\nYour expertise includes:\n- Manufacturing processes (CNC machining, welding, casting, forging, sheet metal fabrication)\n- Materials (metals, plastics, composites)\n- Quality standards (ISO, ASTM, DIN)\n- Production capabilities and lead times\n- Technical specifications and drawings\n- Order status and quotation information\n\nCommunication style:\n- Professional and courteous\n- Clear and concise\n- Technical when needed, but accessible to non-experts\n- Proactive in offering relevant information\n- Patient with follow-up questions", "{\n  \"weather\": \"I'm specialized in manufacturing assistance and don't have information about weather. However, I can help you with material selection, manufacturing processes, or order status. What would you like to know about our manufacturing services?\",\n  \"competitor\": \"I focus on helping you with Maliev's manufacturing services and capabilities. I'd be happy to discuss our offerings, pricing, or how we can meet your manufacturing needs. What specific requirements do you have?\",\n  \"general\": \"I'm here to assist with manufacturing-related questions. I can help you with:\\n- Material selection and specifications\\n- Manufacturing process recommendations\\n- Order status and quotations\\n- Technical drawings and requirements\\nWhat would you like to know about our manufacturing services?\"\n}" });
        }
    }
}
