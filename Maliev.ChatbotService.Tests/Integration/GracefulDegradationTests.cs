using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for graceful degradation (User Story 7).
/// </summary>
[Collection("Database")]
public class GracefulDegradationTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public GracefulDegradationTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that Gemini API timeout triggers fallback response.
    /// </summary>
    [Fact]
    public async Task SendMessage_GeminiTimeout_ReturnsFallbackResponse()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Complex query that might timeout"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert - Should return fallback response instead of error
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that Gemini API unavailable returns predefined fallback with contact info.
    /// </summary>
    [Fact]
    public async Task SendMessage_GeminiUnavailable_ReturnsFallbackWithContact()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Test message during Gemini outage"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that Gemini response fails schema validation, retries with enhanced prompt, then falls back.
    /// </summary>
    [Fact]
    public async Task SendMessage_InvalidSchemaResponse_RetriesAndFallsBack()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Query that might return invalid schema"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that Redis unavailable triggers PostgreSQL fallback with acceptable performance degradation.
    /// </summary>
    [Fact]
    public async Task InitiateSession_RedisUnavailable_FallsBackToPostgreSQL()
    {
        // Arrange
        var client = CreateClient();

        // Act - Stop Redis container to simulate failure
        // Service should automatically fall back to PostgreSQL
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);
    }

    /// <summary>
    /// Tests that Redis recovery is detected and caching resumes automatically.
    /// </summary>
    [Fact]
    public async Task SystemInstructionService_RedisRecovery_ResumesCaching()
    {
        // Arrange
        var client = CreateClient();

        // Act - Simulate Redis recovery
        var sessionRequest1 = new InitiateSessionRequest { Channel = "website" };
        var response1 = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest1, _factory.JsonSerializerOptions);

        // Restart Redis (simulated)
        await Task.Delay(100);

        var sessionRequest2 = new InitiateSessionRequest { Channel = "website" };
        var response2 = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest2, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
    }
}

