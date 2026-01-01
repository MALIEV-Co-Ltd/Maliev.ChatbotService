using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for Message repository CRUD operations and session message retrieval.
/// </summary>
[Collection("Database")]
public class MessageRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public MessageRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that a message can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidMessage_ReturnsCreatedMessage()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
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

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "What manufacturing services do you offer?",
            ContentType = ContentType.Text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var created = await messageRepo.CreateAsync(message);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(message.Content, created.Content);
        Assert.Equal(MessageRole.User, created.Role);
        Assert.Equal(ContentType.Text, created.ContentType);
    }

    /// <summary>
    /// Tests that all messages for a session can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetMessagesBySessionIdAsync_MultipleMessages_ReturnsAllInOrder()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
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

        var message1 = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "สวัสดี",
            ContentType = ContentType.Text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await Task.Delay(10);

        var message2 = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.Assistant,
            Content = "สวัสดีครับ",
            ContentType = ContentType.Text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await messageRepo.CreateAsync(message1);
        await messageRepo.CreateAsync(message2);

        // Act
        var messages = await messageRepo.GetMessagesBySessionIdAsync(session.Id);

        // Assert
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count());
        Assert.Equal(message1.Id, messages.First().Id);
        Assert.Equal(message2.Id, messages.Last().Id);
    }

    /// <summary>
    /// Tests that multimodal messages with metadata are stored correctly.
    /// </summary>
    [Fact]
    public async Task CreateAsync_MultimodalMessage_StoresMetadata()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
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

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Please analyze this image",
            ContentType = ContentType.Image,
            MetadataJson = "{\"imageUrl\":\"https://example.com/image.jpg\",\"fileSize\":8500000}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var created = await messageRepo.CreateAsync(message);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(ContentType.Image, created.ContentType);
        Assert.Contains("imageUrl", created.MetadataJson);
    }

    /// <summary>
    /// Tests that system messages can be stored.
    /// </summary>
    [Fact]
    public async Task CreateAsync_SystemMessage_StoredSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
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
            Channel = Channel.WhatsApp,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.System,
            Content = "Session initiated successfully",
            ContentType = ContentType.Text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var created = await messageRepo.CreateAsync(message);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(MessageRole.System, created.Role);
    }

    /// <summary>
    /// Tests that messages can be retrieved with pagination.
    /// </summary>
    [Fact]
    public async Task GetMessagesBySessionIdAsync_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
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
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        // Create 25 messages
        for (int i = 0; i < 25; i++)
        {
            var message = new Message
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"Message {i}",
                ContentType = ContentType.Text,
                CreatedAt = DateTimeOffset.UtcNow.AddMilliseconds(i)
            };
            await messageRepo.CreateAsync(message);
        }

        // Act
        var allMessages = await messageRepo.GetMessagesBySessionIdAsync(session.Id);
        var firstPage = allMessages.Take(10).ToList();
        var secondPage = allMessages.Skip(10).Take(10).ToList();

        // Assert
        Assert.Equal(10, firstPage.Count);
        Assert.Equal(10, secondPage.Count);
    }
}

