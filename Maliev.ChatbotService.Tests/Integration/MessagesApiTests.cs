using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

[Collection("Database")]
public class MessagesApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public MessagesApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> CreateSessionAsync(HttpClient client)
    {
        var request = new InitiateSessionRequest
        {
            Channel = "line",
            ExternalUserId = "U123456",
            Language = "en"
        };
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);
        var result = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        return result!.SessionId;
    }

    [Fact]
    public async Task SendMessage_WithValidSession_ReturnsResponse()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "Hello, I need pricing for FDM printing"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.MessageId);
    }

    [Fact]
    public async Task SendMessage_WithThaiMessage_ReturnsThaiResponse()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "สวัสดีครับ"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("th", result.Language);
    }

    [Fact]
    public async Task SendMessage_WithInvalidSession_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new SendMessageRequest
        {
            SessionId = Guid.NewGuid(),
            Content = "Hello"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_WithExternalCallbackUrl_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "Hello",
            CallbackUrl = "https://attacker.example/callback"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_WithLoopbackCallbackUrl_InTesting_ReturnsResponse()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "Check quotation Q-2026-000001",
            CallbackUrl = $"https://localhost:9443/api/v1/chat/callback/{sessionId}/thinking?token=test"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.MessageId);
    }

    [Fact]
    public async Task SendMessage_WithEmptyContent_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = ""
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendMessage_WithAttachments_ReturnsResponse()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "What is in this image?",
            Attachments = new List<Attachment>
            {
                new Attachment
                {
                    Type = "image",
                    Url = "https://example.com/image.png",
                    MimeType = "image/png",
                    SizeBytes = 1024
                }
            }
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
    }
}
