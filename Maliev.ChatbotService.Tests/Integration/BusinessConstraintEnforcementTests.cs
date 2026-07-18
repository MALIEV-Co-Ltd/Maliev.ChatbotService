using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for business context enforcement (User Story 4).
/// </summary>
/// <summary>
/// Integration tests for business context enforcement (User Story 4).
/// </summary>
[Collection("Database")]
public class BusinessConstraintEnforcementTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public BusinessConstraintEnforcementTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private HttpClient CreateClient() => _factory.CreateClient();
    /// <summary>
    /// Tests that weather question receives professional rejection with manufacturing redirection.
    /// </summary>
    [Fact]
    public async Task SendMessage_WeatherQuestion_ReceivesProfessionalRejection()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What's the weather like today?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Contains("manufacturing", message.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weather", message.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that competitor services question receives polite decline.
    /// </summary>
    [Fact]
    public async Task SendMessage_CompetitorQuestion_ReceivesPoliteDecline()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Tell me about CompetitorCorp's pricing"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Contains("our", message.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that repeated off-topic requests maintain same professional boundary.
    /// </summary>
    [Fact]
    public async Task SendMessage_RepeatedOffTopic_MaintainsProfessionalBoundary()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        // Act - Send multiple off-topic requests
        var messages = new List<MessageResponse>();
        for (int i = 0; i < 3; i++)
        {
            var messageRequest = new SendMessageRequest
            {
                SessionId = session!.SessionId,
                Content = "Tell me a joke"
            };
            var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);
            var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
            messages.Add(message!);
        }

        // Assert
        Assert.All(messages, m =>
        {
            Assert.Contains("manufacturing", m.Content, StringComparison.OrdinalIgnoreCase);
        });
    }
}

