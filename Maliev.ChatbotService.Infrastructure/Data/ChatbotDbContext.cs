using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Data;

/// <summary>
/// Database context for the Chatbot Service.
/// </summary>
public class ChatbotDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatbotDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public ChatbotDbContext(DbContextOptions<ChatbotDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the UserProfiles DbSet.
    /// </summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>
    /// Gets or sets the ConversationSessions DbSet.
    /// </summary>
    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();

    /// <summary>
    /// Gets or sets the Messages DbSet.
    /// </summary>
    public DbSet<Message> Messages => Set<Message>();

    /// <summary>
    /// Gets or sets the ConversationSummaries DbSet.
    /// </summary>
    public DbSet<ConversationSummary> ConversationSummaries => Set<ConversationSummary>();

    /// <summary>
    /// Gets or sets the UserMemories DbSet.
    /// </summary>
    public DbSet<UserMemory> UserMemories => Set<UserMemory>();

    /// <summary>
    /// Gets or sets the SystemInstructions DbSet.
    /// </summary>
    public DbSet<SystemInstruction> SystemInstructions => Set<SystemInstruction>();

    /// <summary>
    /// Gets or sets the IdentityLinks DbSet.
    /// </summary>
    public DbSet<IdentityLink> IdentityLinks => Set<IdentityLink>();

    /// <summary>
    /// Gets or sets the OperationLogs DbSet.
    /// </summary>
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    /// <summary>
    /// Gets or sets the SearchDomainLogs DbSet.
    /// </summary>
    public DbSet<SearchDomainLog> SearchDomainLogs => Set<SearchDomainLog>();

    /// <summary>
    /// Gets or sets the FallbackResponseTemplates DbSet.
    /// </summary>
    public DbSet<FallbackResponseTemplate> FallbackResponseTemplates => Set<FallbackResponseTemplate>();

    /// <summary>
    /// Configures the model for this context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationSessionConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationSummaryConfiguration());
        modelBuilder.ApplyConfiguration(new UserMemoryConfiguration());
        modelBuilder.ApplyConfiguration(new SystemInstructionConfiguration());
        modelBuilder.ApplyConfiguration(new IdentityLinkConfiguration());
        modelBuilder.ApplyConfiguration(new OperationLogConfiguration());
        modelBuilder.ApplyConfiguration(new SearchDomainLogConfiguration());
        modelBuilder.ApplyConfiguration(new FallbackResponseTemplateConfiguration());

        // Seed default system instruction
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var defaultInstructionId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        modelBuilder.Entity<SystemInstruction>().HasData(
            new SystemInstruction
            {
                Id = defaultInstructionId,
                Name = "Manufacturing Chatbot - Default",
                PersonaDefinition = @"You are Mali, a helpful and knowledgeable female AI assistant for Maliev Manufacturing Company. You specialize in manufacturing processes, materials, and customer inquiries about our services.

Your expertise includes:
- Manufacturing processes (CNC machining, welding, casting, forging, sheet metal fabrication)
- Materials (metals, plastics, composites)
- Quality standards (ISO, ASTM, DIN)
- Production capabilities and lead times
- Technical specifications and drawings
- Order status and quotation information

Communication style:
- Professional, warm, and courteous
- Clear and concise
- Technical when needed, but accessible to non-experts
- Proactive in offering relevant information
- Patient and supportive with follow-up questions",
                BusinessConstraints = @"STRICT RULES - YOU MUST FOLLOW THESE:

1. ONLY discuss topics related to manufacturing, materials, processes, orders, and Maliev company services
2. REJECT politely any questions about:
   - Weather, sports, entertainment, politics
   - Personal advice
   - Competitor services or pricing
   - Non-manufacturing topics

3. When rejecting off-topic questions, ALWAYS redirect to manufacturing topics like:
   ""I'm Mali, and I'm specialized in manufacturing assistance. I can help you with:
   - Material selection and specifications
   - Manufacturing process recommendations
   - Order status and quotations
   - Technical drawings and requirements
   What would you like to know about our manufacturing services?""

4. For internal agents with CRM access:
   - Provide quotation and order status details
   - Offer quick action buttons (Send Reminder, Update Status, etc.)
   - Access customer history

5. NEVER:
   - Share confidential company information
   - Make commitments without proper authorization
   - Provide competitor pricing or comparison",
                AllowedTopics = "manufacturing,materials,processes,orders,quotations,technical specifications,quality standards,production capabilities,lead times,CNC machining,welding,casting,forging,sheet metal fabrication,metals,plastics,composites,ISO standards,ASTM standards,DIN standards",
                RejectionTemplates = @"{
  ""weather"": ""ฉันชื่อมะลิค่ะ เป็นผู้ช่วยด้านงานผลิตโดยเฉพาะ ฉันไม่มีข้อมูลเกี่ยวกับสภาพอากาศ แต่สามารถช่วยคุณเลือกวัสดุ กระบวนการผลิต หรือตรวจสอบสถานะคำสั่งซื้อได้นะคะ คุณต้องการทราบข้อมูลอะไรเกี่ยวกับบริการด้านการผลิตของเราดีคะ?"",
  ""competitor"": ""มะลิเน้นการช่วยเหลือเกี่ยวกับบริการและขีดความสามารถในการผลิตของ Maliev ค่ะ ยินดีที่จะพูดคุยเกี่ยวกับข้อเสนอ ราคา หรือวิธีที่เราจะตอบสนองความต้องการด้านการผลิตของคุณนะคะ คุณมีข้อกำหนดเฉพาะด้านใดบ้างคะ?"",
  ""general"": ""มะลิอยู่ที่นี่เพื่อช่วยตอบคำถามเกี่ยวกับการผลิตค่ะ ฉันสามารถช่วยคุณในเรื่อง:\n- การเลือกวัสดุและข้อมูลเฉพาะทางเทคนิค\n- คำแนะนำด้านกระบวนการผลิต\n- สถานะคำสั่งซื้อและใบเสนอราคา\n- แบบวาดทางเทคนิคและข้อกำหนดต่างๆ\nคุณต้องการทราบข้อมูลอะไรเกี่ยวกับบริการด้านการผลิตของเราดีคะ?""
}",
                IsActive = true,
                Version = 1
            }
        );

        modelBuilder.Entity<FallbackResponseTemplate>().HasData(
            new FallbackResponseTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                ScenarioType = "UnexpectedError",
                Language = Domain.Enums.Language.English,
                ResponseText = "I'm Mali, and I apologize, but I encountered an unexpected error while processing your request. Please try again in a few moments. If the issue persists, you can contact our support team at support@maliev.com.",
                IsActive = true,
                Priority = 100,
                CreatedAt = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)
            },
            new FallbackResponseTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                ScenarioType = "UnexpectedError",
                Language = Domain.Enums.Language.Thai,
                ResponseText = "มะลิขออภัยด้วยนะคะ เกิดข้อผิดพลาดที่ไม่คาดคิดขณะประมวลผลคำขอของคุณ โปรดลองอีกครั้งในอีกสักครู่ หากปัญหายังคงอยู่ คุณสามารถติดต่อทีมสนับสนุนของเราได้ที่ support@maliev.com ค่ะ",
                IsActive = true,
                Priority = 100,
                CreatedAt = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)
            }
        );
    }
}
