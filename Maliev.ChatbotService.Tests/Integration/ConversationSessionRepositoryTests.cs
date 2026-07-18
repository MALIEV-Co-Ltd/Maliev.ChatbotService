using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for ConversationSession repository CRUD operations and session expiry queries.
/// </summary>
[Collection("Database")]
public class ConversationSessionRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public ConversationSessionRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Initializes the test.
    /// </summary>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
    /// <summary>
    /// Tests that a conversation session can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidSession_ReturnsCreatedSession()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

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

        // Act
        var created = await sessionRepo.CreateAsync(session);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(session.UserProfileId, created.UserProfileId);
        Assert.Equal(Channel.Website, created.Channel);
        Assert.Equal(Language.English, created.Language);
        Assert.Equal(SessionStatus.Active, created.Status);
    }

    /// <summary>
    /// Tests that a session can be retrieved by ID.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Line,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.Thai,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        // Act
        var retrieved = await sessionRepo.GetByIdAsync(session.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
        Assert.Equal(Channel.Line, retrieved.Channel);
        Assert.Equal(Language.Thai, retrieved.Language);
    }

    /// <summary>
    /// Tests that active sessions can be retrieved for a user.
    /// </summary>
    [Fact]
    public async Task GetActiveSessionByUserAndChannelAsync_ExistingActiveSession_ReturnsSession()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Facebook,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        // Act
        var retrieved = await sessionRepo.GetActiveSessionByUserAndChannelAsync(
            userProfile.Id, PlatformName.Facebook);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
        Assert.Equal(SessionStatus.Active, retrieved.Status);
    }

    /// <summary>
    /// Tests that expired sessions can be queried.
    /// </summary>
    [Fact]
    public async Task GetExpiredSessionsAsync_SessionsPastExpiryTime_ReturnsExpiredSessions()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var expiredSession = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.WhatsApp,
            StartTime = DateTimeOffset.UtcNow.AddDays(-2),
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(expiredSession);

        // Act
        var expiredSessions = await sessionRepo.GetExpiredSessionsAsync();

        // Assert
        Assert.NotEmpty(expiredSessions);
        Assert.Contains(expiredSessions, s => s.Id == expiredSession.Id);
    }

    /// <summary>
    /// Tests that a session can be updated to closed status.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_CloseSession_UpdatesStatusSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Instagram,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.Thai,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        // Act
        session.Status = SessionStatus.Closed;
        await sessionRepo.UpdateAsync(session);

        // Assert
        var updated = await sessionRepo.GetByIdAsync(session.Id);
        Assert.NotNull(updated);
        Assert.Equal(SessionStatus.Closed, updated.Status);
    }

    /// <summary>
    /// Tests that a session with linked summary can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_SessionWithSummary_IncludesSummary()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var summaryRepo = scope.ServiceProvider.GetRequiredService<IConversationSummaryRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Intranet,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.English,
            Status = SessionStatus.Closed
        };
        await sessionRepo.CreateAsync(session);

        var summary = new ConversationSummary
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UserProfileId = userProfile.Id,
            StructuredSummary = "{\"topics\":[\"manufacturing\"],\"decisions\":[],\"preferences\":[],\"entities\":[],\"intentCategories\":[\"inquiry\"],\"unresolvedQuestions\":[]}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await summaryRepo.CreateAsync(summary);

        session.SummaryId = summary.Id;
        await sessionRepo.UpdateAsync(session);

        // Act
        var retrieved = await sessionRepo.GetByIdAsync(session.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(summary.Id, retrieved.SummaryId);
    }

    /// <summary>
    /// Tests that last activity time is updated correctly.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_UpdateLastActivityTime_ExtendsExpiryTime()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

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
        await sessionRepo.CreateAsync(session);

        var originalExpiresAt = session.ExpiresAt;

        // Act
        await Task.Delay(100); // Small delay to ensure timestamp difference
        session.LastActivityAt = DateTimeOffset.UtcNow;
        session.ExpiresAt = session.LastActivityAt.AddDays(1);
        await sessionRepo.UpdateAsync(session);

        // Assert
        var updated = await sessionRepo.GetByIdAsync(session.Id);
        Assert.NotNull(updated);
        Assert.True(updated.ExpiresAt > originalExpiresAt);
    }

    /// <summary>
    /// Tests that all sessions for a user can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetSessionsByUserIdAsync_MultipleSessionsForUser_ReturnsAllSessions()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var session1 = new ConversationSession
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

        var session2 = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Line,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.Thai,
            Status = SessionStatus.Closed
        };

        await sessionRepo.CreateAsync(session1);
        await sessionRepo.CreateAsync(session2);

        // Act
        var sessions = await sessionRepo.GetSessionsByUserIdAsync(userProfile.Id);

        // Assert
        Assert.NotNull(sessions);
        Assert.Equal(2, sessions.Count());
        Assert.Contains(sessions, s => s.Channel == Channel.Website);
        Assert.Contains(sessions, s => s.Channel == Channel.Line);
    }
}

