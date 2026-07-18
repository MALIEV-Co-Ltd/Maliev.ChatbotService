using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Tests.Infrastructure;
using Maliev.ChatbotService.Api.Models.Webhooks;
using System.Text.Json;

namespace Maliev.ChatbotService.Tests.Integration;

[Collection("Database")]
public class WebhooksApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public WebhooksApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HandleLineWebhook_WithValidRequest_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var webhookEvent = new
        {
            events = new[]
            {
                new
                {
                    type = "message",
                    timestamp = 1234567890000,
                    source = new { userId = "U123456" },
                    message = new { type = "text", text = "Hello" },
                    replyToken = "reply123"
                }
            }
        };

        var json = JsonSerializer.Serialize(webhookEvent);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/chatbot/v1/webhooks/line", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HandleLineWebhook_WithNonTextMessage_SkipsProcessing()
    {
        var client = _factory.CreateClient();
        var webhookEvent = new
        {
            events = new[]
            {
                new
                {
                    type = "message",
                    timestamp = 1234567890000,
                    source = new { userId = "U123456" },
                    message = new { type = "image", id = "msg123" }
                }
            }
        };

        var json = JsonSerializer.Serialize(webhookEvent);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/chatbot/v1/webhooks/line", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HandleMetaWebhookVerification_WithValidToken_ReturnsForbiddenWhenNoTokenConfigured()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/chatbot/v1/webhooks/meta?hub.mode=subscribe&hub.verify_token=test-verify-token&hub.challenge=test-challenge");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HandleMetaWebhookVerification_WithInvalidToken_ReturnsForbidden()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/chatbot/v1/webhooks/meta?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge=test-challenge");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HandleMetaWebhook_WithValidRequest_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var webhookEvent = new
        {
            @object = "page",
            entry = new[]
            {
                new
                {
                    id = "page123",
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "user123" },
                            recipient = new { id = "page123" },
                            message = new { text = "Hello from Facebook" },
                            timestamp = 1234567890000
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(webhookEvent);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/chatbot/v1/webhooks/meta", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HandleMetaWebhook_WithWhatsAppChannel_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var webhookEvent = new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "whatsapp123",
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
                                        sender = new { id = "user123" },
                                        message = new { text = "Hello from WhatsApp" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(webhookEvent);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/chatbot/v1/webhooks/meta", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HandleMetaWebhook_WithInstagramChannel_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var webhookEvent = new
        {
            @object = "instagram",
            entry = new[]
            {
                new
                {
                    id = "instagram123",
                    messaging = new[]
                    {
                        new
                        {
                            sender = new { id = "user123" },
                            message = new { text = "Hello from Instagram" }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(webhookEvent);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/chatbot/v1/webhooks/meta", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
