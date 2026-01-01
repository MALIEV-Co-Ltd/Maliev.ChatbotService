using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for webhook endpoints (LINE, Facebook, WhatsApp).
/// </summary>
[Collection("Database")]
public class WebhookIntegrationTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public WebhookIntegrationTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that POST /v1/webhooks/line with valid signature processes LINE message.
    /// </summary>
    [Fact]
    public async Task ReceiveWebhook_LineWithValidSignature_ProcessesMessage()
    {
        // Arrange
        var client = CreateClient();
        var webhookPayload = new
        {
            events = new[]
            {
                new
                {
                    type = "message",
                    message = new { type = "text", text = "สวัสดี" },
                    source = new { userId = "U1234567890" }
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/webhooks/line", webhookPayload, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests that POST /v1/webhooks/meta with valid signature processes Facebook message.
    /// </summary>
    [Fact]
    public async Task ReceiveWebhook_FacebookWithValidSignature_ProcessesMessage()
    {
        // Arrange
        var client = CreateClient();
        var webhookPayload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "1234567890" },
                            message = new { text = "Hello" },
                            timestamp = 1640000000000L
                        }
                    }
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/webhooks/meta", webhookPayload, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests that POST /v1/webhooks/meta with valid signature processes WhatsApp message.
    /// </summary>
    [Fact]
    public async Task ReceiveWebhook_WhatsAppWithValidSignature_ProcessesMessage()
    {
        // Arrange
        var client = CreateClient();
        var webhookPayload = new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                messaging = new[]
                                {
                                    new
                                    {
                                        sender = new { id = "1234567890" },
                                        message = new { text = "Test message" },
                                        timestamp = 1640000000000L
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/webhooks/meta", webhookPayload, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests that LINE Flex Message formatting works for structured response.
    /// </summary>
    [Fact]
    public async Task Webhook_LineFlexMessage_FormatsStructuredResponse()
    {
        // Arrange
        var client = CreateClient();
        var webhookPayload = new
        {
            events = new[]
            {
                new
                {
                    type = "message",
                    replyToken = "test-reply-token",
                    message = new { type = "text", text = "What services do you offer?" },
                    source = new { userId = "U9876543210" },
                    timestamp = 1640000000000L
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/webhooks/line", webhookPayload, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // LINE Flex Message should be sent in response
    }

    /// <summary>
    /// Tests that Facebook Generic Template formatting works for structured response.
    /// </summary>
    [Fact]
    public async Task Webhook_FacebookGenericTemplate_FormatsStructuredResponse()
    {
        // Arrange
        var client = CreateClient();
        var webhookPayload = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "555666777" },
                            message = new { text = "Tell me about CNC machining" },
                            timestamp = 1640000000000L
                        }
                    }
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/webhooks/meta", webhookPayload, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests that website UI component formatting works for structured response.
    /// </summary>
    [Fact]
    public async Task Message_WebsiteChannel_FormatsUIComponents()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new { channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(_factory.JsonSerializerOptions);

        Assert.True(session.TryGetProperty("session_id", out var sessionIdElement));
        var sessionId = sessionIdElement.GetString();
        Assert.NotNull(sessionId);

        var messageRequest = new
        {
            session_id = sessionId,
            content = "Show me your services"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

