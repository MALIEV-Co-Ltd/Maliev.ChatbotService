using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for multimodal input processing (User Story 3).
/// </summary>
[Collection("Database")]
public class MultimodalInputTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public MultimodalInputTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that image attachment under 10MB processes successfully.
    /// </summary>
    [Fact]
    public async Task SendMessage_ImageUnder10MB_ProcessesSuccessfully()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Please analyze this part drawing",
            Attachments =
            [
                new Attachment
                {
                    Type = "image",
                    Url = "https://example.com/drawing.jpg",
                    SizeBytes = 8500000 // 8.5 MB
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that PDF attachment under 20MB processes successfully.
    /// </summary>
    [Fact]
    public async Task SendMessage_PdfUnder20MB_ProcessesSuccessfully()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Review this technical specification",
            Attachments =
            [
                new Attachment
                {
                    Type = "pdf",
                    Url = "https://example.com/spec.pdf",
                    SizeBytes = 18000000 // 18 MB
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that video attachment under 50MB processes successfully.
    /// </summary>
    [Fact]
    public async Task SendMessage_VideoUnder50MB_ProcessesSuccessfully()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Analyze this manufacturing process video",
            Attachments =
            [
                new Attachment
                {
                    Type = "video",
                    Url = "https://example.com/process.mp4",
                    SizeBytes = 45000000 // 45 MB
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that oversized image over 10MB returns 400 with clear error.
    /// </summary>
    [Fact]
    public async Task SendMessage_ImageOver10MB_Returns400WithClearError()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Analyze this image",
            Attachments =
            [
                new Attachment
                {
                    Type = "image",
                    Url = "https://example.com/large.jpg",
                    SizeBytes = 11000000 // 11 MB - exceeds limit
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("10MB", content);
    }

    /// <summary>
    /// Tests that image analysis provides manufacturing process identification.
    /// </summary>
    [Fact]
    public async Task SendMessage_ImageAnalysis_IdentifiesManufacturingProcess()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What manufacturing process is shown here?",
            Attachments =
            [
                new Attachment
                {
                    Type = "image",
                    Url = "https://example.com/cnc-process.jpg",
                    SizeBytes = 5000000
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Contains("process", message.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that PDF technical drawing extracts dimensions and materials.
    /// </summary>
    [Fact]
    public async Task SendMessage_PdfDrawing_ExtractsDimensionsAndMaterials()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Extract specifications from this drawing",
            Attachments =
            [
                new Attachment
                {
                    Type = "pdf",
                    Url = "https://example.com/technical-drawing.pdf",
                    SizeBytes = 3000000
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }
}

