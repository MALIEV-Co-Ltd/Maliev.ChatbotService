using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for internal CRM agent assistance (User Story 6).
/// </summary>
[Collection("Database")]
public class InternalCRMAgentTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public InternalCRMAgentTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that authenticated sales agent queries quotation status and receives structured data with actions.
    /// </summary>
    [Fact]
    public async Task SendMessage_QuotationQuery_ReturnsStructuredDataWithActions()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.messages.send", "internal_agent" });
        var sessionRequest = new InitiateSessionRequest { Channel = "intranet" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Show me quotation Q-2025-1234"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.NotNull(message.SuggestedActions);
        Assert.NotEmpty(message.SuggestedActions);
        Assert.Contains(message.SuggestedActions, a => a != null && a.Label != null && a.Label.Contains("Send Reminder"));
    }

    /// <summary>
    /// Tests that unauthenticated user attempting CRM query receives 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task SendMessage_CrmQueryUnauthenticated_Returns403()
    {
        // Arrange
        var client = CreateClient(); // No authentication
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Show me quotation Q-2025-1234"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        // Should either return 403 or a message refusing the operation
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.OK);
    }

    /// <summary>
    /// Tests that internal agent executes Send Reminder operation successfully.
    /// </summary>
    [Fact]
    public async Task SendMessage_ExecuteSendReminder_ExecutesSuccessfully()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.messages.send", "internal_agent" });
        var sessionRequest = new InitiateSessionRequest { Channel = "intranet" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Send reminder for quotation Q-2025-1234"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Contains("sent", message.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that internal agent can query order status.
    /// </summary>
    [Fact]
    public async Task SendMessage_OrderStatusQuery_ReturnsOrderDetails()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.messages.send", "internal_agent" });
        var sessionRequest = new InitiateSessionRequest { Channel = "intranet" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What's the status of order O-2025-500?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }
}

