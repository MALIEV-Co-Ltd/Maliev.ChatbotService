using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for persistent user preferences (User Story 5).
/// </summary>
[Collection("Database")]
public class UserPreferencesTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public UserPreferencesTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private HttpClient CreateClient() => _factory.CreateClient();
    private HttpClient CreateAuthenticatedClient(string[]? permissions = null) => _factory.CreateAuthenticatedClient("test-user", permissions);
    /// <summary>
    /// Tests that GET /v1/users/me/preferences returns paginated preference list.
    /// </summary>
    [Fact]
    public async Task GetPreferences_ValidRequest_ReturnsPaginatedList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        // Create session and message first (required for UserMemory FK constraint)
        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Test preferences",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepo.CreateAsync(message);

        for (int i = 0; i < 25; i++)
        {
            var memory = new UserMemory
            {
                Id = Guid.NewGuid(),
                UserProfileId = userProfile.Id,
                Key = $"Preference{i}",
                Value = $"{{\"value\":\"test{i}\"}}",
                Confidence = 0.9,
                SourceMessageId = message.Id,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            await memoryRepo.CreateAsync(memory);
        }

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());

        // Act
        var response = await client.GetAsync("/chatbot/v1/users/me/preferences?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedPreferencesResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal(10, result.Data.Count());
        Assert.Equal(1, result.Meta.Page);
        Assert.Equal(25, result.Meta.TotalCount);
    }

    /// <summary>
    /// Tests that DELETE /v1/users/me/data with scope=preferences requires confirmation.
    /// </summary>
    [Fact]
    public async Task DeleteUserData_WithoutConfirmation_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());

        // Act
        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=preferences&confirm=false");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that stated preference is stored with high confidence.
    /// </summary>
    [Fact]
    public async Task SendMessage_StatedPreference_StoresWithHighConfidence()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "I prefer stainless steel 304 for all my parts"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Preference should be stored with confidence > 0.8
    }

    /// <summary>
    /// Tests that new session retrieves stored preferences and offers to reuse.
    /// </summary>
    [Fact]
    public async Task InitiateSession_WithStoredPreferences_OffersToReuse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        // Create a session and message first (required for UserMemory FK constraint)
        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "I prefer aluminum 6061",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepo.CreateAsync(message);

        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "MaterialPreference",
            Value = "{\"material\":\"aluminum 6061\"}",
            Confidence = 0.95,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
        await memoryRepo.CreateAsync(memory);

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);

        // Assert
        var sessionResponse = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(sessionResponse);
        Assert.Contains("aluminum", sessionResponse.WelcomeMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that user deletion request with confirmation deletes selected scope.
    /// </summary>
    [Fact]
    public async Task DeleteUserData_WithConfirmation_DeletesSelectedScope()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        // Create session and message first (required for UserMemory FK constraint)
        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Test message",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepo.CreateAsync(message);

        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "TestPreference",
            Value = "{\"test\":\"value\"}",
            Confidence = 0.9,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
        await memoryRepo.CreateAsync(memory);

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());

        // Act
        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=preferences&confirm=true");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var memories = await memoryRepo.GetMemoriesByUserIdAsync(userProfile.Id);
        Assert.Empty(memories);
    }

    /// <summary>
    /// Tests that DELETE /v1/users/me/data with scope=history deletes messages.
    /// </summary>
    [Fact]
    public async Task DeleteUserData_HistoryScope_DeletesMessages()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

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
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        await messageRepo.CreateAsync(new Message { Id = Guid.NewGuid(), SessionId = session.Id, Role = MessageRole.User, Content = "Msg 1" });

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());

        // Act
        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=history&confirm=true");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var messages = await messageRepo.GetMessagesBySessionIdAsync(session.Id);
        Assert.Empty(messages);
    }

    /// <summary>
    /// Tests that DELETE /v1/users/me/data with scope=all deletes everything.
    /// </summary>
    [Fact]
    public async Task DeleteUserData_AllScope_DeletesEverything()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

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
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        var message = new Message { Id = Guid.NewGuid(), SessionId = session.Id, Role = MessageRole.User, Content = "Msg 1" };
        await messageRepo.CreateAsync(message);

        await memoryRepo.CreateAsync(new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "Test",
            Value = "{}",
            Confidence = 0.9,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        });

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());

        // Act
        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=all&confirm=true");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var messages = await messageRepo.GetMessagesBySessionIdAsync(session.Id);
        Assert.Empty(messages);
        var memories = await memoryRepo.GetMemoriesByUserIdAsync(userProfile.Id);
        Assert.Empty(memories);
    }
}

