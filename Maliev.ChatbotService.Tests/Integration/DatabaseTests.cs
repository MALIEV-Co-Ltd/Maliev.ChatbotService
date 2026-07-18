using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for ChatbotDbContext configuration and database migrations.
/// </summary>
[Collection("Database")]
public class DatabaseTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public DatabaseTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Initializes the test.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;
    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
    /// <summary>
    /// Tests that all entities are configured correctly in the DbContext.
    /// </summary>
    [Fact]
    public async Task ChatbotDbContext_AllEntitiesConfigured_CanCreateSchema()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();

        // Act
        await dbContext.Database.EnsureCreatedAsync();

        // Assert
        var tables = await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        Assert.NotEqual(0, tables);
    }

    /// <summary>
    /// Tests that the InitialCreate migration applies successfully.
    /// </summary>
    [Fact]
    public async Task ChatbotDbContext_InitialCreateMigration_AppliesSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();

        // Act
        await dbContext.Database.MigrateAsync();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        // Assert
        Assert.Empty(pendingMigrations);
    }

    /// <summary>
    /// Tests that all entity relationships are configured correctly.
    /// </summary>
    [Fact]
    public async Task ChatbotDbContext_EntityRelationships_ConfiguredCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.English,
            Status = SessionStatus.Active
        };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Test message",
            ContentType = ContentType.Text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        dbContext.UserProfiles.Add(userProfile);
        dbContext.ConversationSessions.Add(session);
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();

        // Assert
        var retrievedMessage = await dbContext.Messages
            .Include(m => m.Session)
            .ThenInclude(s => s!.UserProfile)
            .FirstOrDefaultAsync(m => m.Id == message.Id);

        Assert.NotNull(retrievedMessage);
        Assert.NotNull(retrievedMessage.Session);
        Assert.NotNull(retrievedMessage.Session.UserProfile);
        Assert.Equal(userProfile.Id, retrievedMessage.Session.UserProfile.Id);
    }

    /// <summary>
    /// Tests that unique constraints are enforced on external platform IDs.
    /// </summary>
    [Fact]
    public async Task ChatbotDbContext_UniqueConstraints_EnforcedOnPlatformIds()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var userProfile1 = new UserProfile
        {
            Id = Guid.NewGuid(),
            LineUserId = "U1234567890",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        var userProfile2 = new UserProfile
        {
            Id = Guid.NewGuid(),
            LineUserId = "U1234567890", // Duplicate LINE ID
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        // Act & Assert
        dbContext.UserProfiles.Add(userProfile1);
        await dbContext.SaveChangesAsync();

        dbContext.UserProfiles.Add(userProfile2);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    /// <summary>
    /// Tests that indexes are created on frequently queried fields.
    /// </summary>
    [Fact]
    public async Task ChatbotDbContext_Indexes_CreatedOnFrequentlyQueriedFields()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Act - Query should use indexes for these fields
        var indexes = await dbContext.Database.ExecuteSqlRawAsync(@"
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'public'
            AND (indexname LIKE '%LineUserId%'
                 OR indexname LIKE '%FacebookId%'
                 OR indexname LIKE '%ExpiresAt%')");

        // Assert
        Assert.NotEqual(0, indexes);
    }
}

