using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Contract;

/// <summary>
/// Contract tests for manufacturing inquiry messages (User Story 1).
/// </summary>
[Collection("Database")]
public class ManufacturingInquiryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public ManufacturingInquiryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that POST /v1/messages with manufacturing inquiry returns structured response with buttons.
    /// </summary>
    [Fact]
    public async Task SendMessage_ManufacturingInquiry_ReturnsStructuredResponseWithButtons()
    {
        // Arrange
        var client = CreateClient();

        // First, initiate a session
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What manufacturing services do you offer?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.NotEqual(Guid.Empty, message.MessageId);
        Assert.False(string.IsNullOrEmpty(message.Content));
        Assert.Equal("assistant", message.Role);
        Assert.NotNull(message.SuggestedActions);
        Assert.NotEmpty(message.SuggestedActions);
    }

    /// <summary>
    /// Tests that language detection switches to Thai when Thai message received.
    /// </summary>
    [Fact]
    public async Task SendMessage_ThaiMessage_SwitchesToThaiLanguage()
    {
        // Arrange
        var client = CreateClient();

        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "บริการผลิตของคุณมีอะไรบ้าง"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Equal("th", message.Language);
    }

    /// <summary>
    /// Tests that business constraint enforcement rejects off-topic weather question.
    /// </summary>
    [Fact]
    public async Task SendMessage_WeatherQuestion_RejectsWithProfessionalRedirection()
    {
        // Arrange
        var client = CreateClient();

        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What's the weather today?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Contains("manufacturing", message.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that unsupported language (Chinese) receives English default response.
    /// </summary>
    [Fact]
    public async Task SendMessage_ChineseLanguage_DefaultsToEnglish()
    {
        // Arrange
        var client = CreateClient();

        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "你们有什么制造服务" // Chinese
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Equal("en", message.Language);
    }

    /// <summary>
    /// Tests that rate limit (100 msg/hr) returns 429 when exceeded.
    /// </summary>
    [Fact]
    public async Task SendMessage_ExceedsRateLimit_Returns429()
    {
        // Arrange
        var client = CreateClient();

        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        // Act - Send 101 messages
        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 101; i++)
        {
            var messageRequest = new SendMessageRequest
            {
                SessionId = session!.SessionId,
                Content = $"Message {i}"
            };
            lastResponse = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);
        }

        // Assert
        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.True(lastResponse.Headers.Contains("Retry-After"));
    }
}

