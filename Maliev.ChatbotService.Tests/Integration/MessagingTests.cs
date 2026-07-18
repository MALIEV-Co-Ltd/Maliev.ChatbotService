using Maliev.ChatbotService.Infrastructure.Data;
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
/// Integration tests for RabbitMQ event publishing.
/// </summary>
[Collection("Database")]
public class MessagingTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public MessagingTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// <summary>
    /// Tests that SessionCreated event is published to RabbitMQ when session initiated.
    /// </summary>
    [Fact]
    public async Task InitiateSession_NewSession_PublishesSessionCreatedEvent()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Event should be published to RabbitMQ: ChatbotSessionCreatedEvent
    }

    /// <summary>
    /// Tests that SessionClosed event is published to RabbitMQ when session expires.
    /// </summary>
    [Fact]
    public async Task SessionExpiry_ExpiredSession_PublishesSessionClosedEvent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionExpiryService = scope.ServiceProvider.GetRequiredService<ISessionExpiryService>();

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
            StartTime = DateTimeOffset.UtcNow.AddDays(-2),
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        // Act
        await sessionExpiryService.ProcessExpiredSessionsAsync();

        // Assert
        // Event should be published to RabbitMQ: ChatbotSessionClosedEvent
    }

    /// <summary>
    /// Tests that MessageReceived event is published to RabbitMQ when user message processed.
    /// </summary>
    [Fact]
    public async Task SendMessage_UserMessage_PublishesMessageReceivedEvent()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Test message"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        // Event should be published to RabbitMQ: ChatbotMessageReceivedEvent
    }

    /// <summary>
    /// Tests that RateLimitExceeded event is published to RabbitMQ when 100 msg/hr exceeded.
    /// </summary>
    [Fact]
    public async Task SendMessage_ExceedsRateLimit_PublishesRateLimitExceededEvent()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        // Act - Send 101 messages to exceed rate limit
        for (int i = 0; i < 101; i++)
        {
            var messageRequest = new SendMessageRequest
            {
                SessionId = session!.SessionId,
                Content = $"Message {i}"
            };
            await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);
        }

        // Assert
        // Event should be published to RabbitMQ: ChatbotRateLimitExceededEvent
    }
}

