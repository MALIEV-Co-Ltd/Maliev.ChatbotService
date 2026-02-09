using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
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
    /// Gets or sets the KnowledgeBase DbSet.
    /// </summary>
    public DbSet<KnowledgeBase> KnowledgeBase => Set<KnowledgeBase>();

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
        modelBuilder.ApplyConfiguration(new KnowledgeBaseConfiguration());

        // Seed default system instruction
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // SystemInstruction seed data has been moved to markdown files in Prompts/ directory.
        // The PromptFileLoaderService handles seeding from markdown files at startup.

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
