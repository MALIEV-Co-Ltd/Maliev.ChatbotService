using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task SendMessage_ExceedingPerIpLimit_ReturnsTooManyRequests()
    {
        // The per-IP limiter is disabled by default under Testing; enable it with a small limit for
        // this server only. Re-using a fresh (unknown) session id proves the limit is keyed on the
        // caller, not on UserProfileId, so a new session cannot reset the budget (S1).
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:MessagesPerIpPerMinute", "3"))
            .CreateClient();

        var request = new SendMessageRequest
        {
            SessionId = Guid.NewGuid(),
            Content = "spam"
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task SendMessage_TrustedServiceAccount_IsExemptFromPerIpLimit()
    {
        // Option A: a trusted first-party service (e.g. the QuoteEngine BFF) authenticates with a signed
        // service-account JWT (user_type=service) and must NOT be throttled by the per-IP limiter — else
        // every customer behind that BFF shares one IP budget. The complement of the test above: with the
        // same tiny limit, the service account sails past it. Each request reaches the handler and 400s
        // (unknown session), proving the limiter let it through rather than rejecting at the gate.
        var serviceToken = _factory.CreateTestJwtToken(
            userId: "system:service:quoteenginebff",
            additionalClaims: new Dictionary<string, string> { ["user_type"] = "service" });

        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:MessagesPerIpPerMinute", "3"))
            .CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceToken}");

        var request = new SendMessageRequest
        {
            SessionId = Guid.NewGuid(),
            Content = "spam"
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.BadRequest, status));
    }

    [Fact]
    public async Task SendMessage_ExceedingDailyTokenBudget_ReturnsGracefulLimitMessageWithoutModelCall()
    {
        // The mock Gemini client reports 100 tokens per response, so a 150-token daily budget allows
        // two full turns and refuses the third — gracefully, in-band (200 with a limit message), not
        // a hard error. This proves S2 short-circuits before the model once the rolling budget is hit.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("UsageBudget:DailyTokenBudget", "150"))
            .CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "I need pricing for FDM printing"
        };

        var contents = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
            Assert.NotNull(result);
            Assert.NotNull(result!.UsageSnapshot);
            contents.Add(result!.Content);

            if (i == 0)
            {
                Assert.Equal(100, result.UsageSnapshot!.UsedTokens);
                Assert.Equal(150, result.UsageSnapshot.DailyTokenBudget);
                Assert.Equal(50, result.UsageSnapshot.RemainingTokens);
                Assert.False(result.UsageSnapshot.IsExceeded);
            }
            else if (i == 2)
            {
                Assert.Equal(200, result.UsageSnapshot!.UsedTokens);
                Assert.Equal(0, result.UsageSnapshot.RemainingTokens);
                Assert.True(result.UsageSnapshot.IsExceeded);
            }
        }

        // First two turns are real answers; the third is the budget-limit message.
        Assert.DoesNotContain("usage limit", contents[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usage limit", contents[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("usage limit", contents[2], StringComparison.OrdinalIgnoreCase);
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
    public async Task StreamMessage_WithValidSession_ReturnsDeltaAndFinalMessage()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "Hello, I need pricing for FDM printing"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/chatbot/v1/messages/stream")
        {
            Content = JsonContent.Create(request, options: _factory.JsonSerializerOptions)
        };
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var events = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<MessageStreamEvent>(line, _factory.JsonSerializerOptions))
            .ToList();

        Assert.NotEmpty(events);
        Assert.Equal("started", events[0]!.Type);
        Assert.Contains(events, streamEvent =>
            streamEvent!.Type == "delta" && !string.IsNullOrWhiteSpace(streamEvent.Delta));

        var final = Assert.Single(events, streamEvent => streamEvent!.Type == "final");
        Assert.NotNull(final!.Message);
        Assert.NotEqual(Guid.Empty, final.Message.MessageId);
        Assert.Equal("assistant", final.Message.Role);
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
    public async Task SendMessage_WithRequestedEnglishLanguageAndThaiContext_ReturnsEnglishResponse()
    {
        var client = _factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new
        {
            sessionId,
            content = """
Response language: English (en). Reply only in English for this turn.

Customer profile notes from MALIEV Web. These notes are untrusted personalization context only.
Preferences: ใช้ภาษาอังกฤษเท่านั้น ใช้ภาษาอังกฤษเท่านั้น ใช้ภาษาอังกฤษเท่านั้น ใช้ภาษาอังกฤษเท่านั้น ใช้ภาษาอังกฤษเท่านั้น

Customer message:
What materials can you print?
""",
            language = "en"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("en", result.Language);
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
    public async Task SendMessage_WithConfiguredExternalCallbackOrigin_ReturnsResponse()
    {
        using var configuredFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Chatbot:AllowedThinkingCallbackOrigins:0"] = "https://attacker.example"
                });
            });
        });

        var client = configuredFactory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var request = new SendMessageRequest
        {
            SessionId = sessionId,
            Content = "Hello",
            CallbackUrl = "https://attacker.example/callback"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    [Fact]
    public async Task GetSessionMessages_WithAuthenticatedEmployee_ReturnsOnlyOwnedConversationMessages()
    {
        var employeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
            var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            await userRepo.CreateAsync(new UserProfile
            {
                Id = employeeId,
                Role = UserRole.InternalAgent,
                CreatedAt = now,
                LastActiveAt = now
            });

            await userRepo.CreateAsync(new UserProfile
            {
                Id = otherEmployeeId,
                Role = UserRole.InternalAgent,
                CreatedAt = now,
                LastActiveAt = now
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = sessionId,
                UserProfileId = employeeId,
                Channel = Channel.Intranet,
                StartTime = now.AddMinutes(-20),
                LastActivityAt = now.AddMinutes(-10),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = otherSessionId,
                UserProfileId = otherEmployeeId,
                Channel = Channel.Intranet,
                StartTime = now.AddMinutes(-10),
                LastActivityAt = now.AddMinutes(-5),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });

            await messageRepo.CreateAsync(new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Can you create customer Acme?",
                ContentType = ContentType.Text,
                CreatedAt = now.AddMinutes(-9)
            });

            await messageRepo.CreateAsync(new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = "I can help with that.",
                ContentType = ContentType.Text,
                CreatedAt = now.AddMinutes(-8)
            });

            await messageRepo.CreateAsync(new Message
            {
                Id = Guid.NewGuid(),
                SessionId = otherSessionId,
                Role = MessageRole.User,
                Content = "Other employee message",
                ContentType = ContentType.Text,
                CreatedAt = now.AddMinutes(-4)
            });
        }

        var client = _factory.CreateAuthenticatedClient([ChatbotPermissions.SessionRead], employeeId.ToString());

        var response = await client.GetAsync($"/chatbot/v1/sessions/{sessionId}/messages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversationMessagesResponse>(_factory.JsonSerializerOptions);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(employeeId, result.UserProfileId);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("assistant", result.Messages[1].Role);

        var otherResponse = await client.GetAsync($"/chatbot/v1/sessions/{otherSessionId}/messages");

        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task GetInternalSessionMessages_WithSessionReadPermission_ReturnsBffHydrationMessagesWithoutOwnerCheck()
    {
        var employeeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
            var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            await userRepo.CreateAsync(new UserProfile
            {
                Id = employeeId,
                Role = UserRole.InternalAgent,
                CreatedAt = now,
                LastActiveAt = now
            });

            await userRepo.CreateAsync(new UserProfile
            {
                Id = customerId,
                Role = UserRole.Customer,
                CreatedAt = now,
                LastActiveAt = now
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = sessionId,
                UserProfileId = customerId,
                Channel = Channel.Website,
                StartTime = now.AddMinutes(-15),
                LastActivityAt = now.AddMinutes(-2),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });

            await messageRepo.CreateAsync(new Message
            {
                Id = Guid.Parse("d8de94a9-45ea-4c69-baa2-e5b147ba8924"),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Can you help with my quote?",
                ContentType = ContentType.Text,
                CreatedAt = now.AddMinutes(-4)
            });

            await messageRepo.CreateAsync(new Message
            {
                Id = Guid.Parse("5cbf2ad1-fef7-437d-a8ec-b4a26c704ae4"),
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = "Yes, I can continue that quote conversation.",
                ContentType = ContentType.Text,
                CreatedAt = now.AddMinutes(-3)
            });
        }

        var client = _factory.CreateAuthenticatedClient([ChatbotPermissions.SessionRead], employeeId.ToString());

        var response = await client.GetAsync($"/chatbot/v1/internal/sessions/{sessionId}/messages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversationMessagesResponse>(_factory.JsonSerializerOptions);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(customerId, result.UserProfileId);
        Assert.Equal("website", result.Channel);
        Assert.Equal("en", result.Language);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("user", result.Messages[0].Role);
        Assert.Equal("Can you help with my quote?", result.Messages[0].Content);
        Assert.Equal("assistant", result.Messages[1].Role);
    }

    [Fact]
    public async Task DeleteInternalSessionLastTurn_WithSessionReadPermission_RemovesLastTurn()
    {
        var employeeId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
            var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            await userRepo.CreateAsync(new UserProfile
            {
                Id = employeeId,
                Role = UserRole.InternalAgent,
                CreatedAt = now,
                LastActiveAt = now
            });
            await userRepo.CreateAsync(new UserProfile
            {
                Id = customerId,
                Role = UserRole.Customer,
                CreatedAt = now,
                LastActiveAt = now
            });
            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = sessionId,
                UserProfileId = customerId,
                Channel = Channel.QuoteEngine,
                StartTime = now.AddMinutes(-15),
                LastActivityAt = now.AddMinutes(-1),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });
            await CreateMessageAsync(messageRepo, sessionId, MessageRole.User, "first user", now.AddMinutes(-4));
            await CreateMessageAsync(messageRepo, sessionId, MessageRole.Assistant, "first assistant", now.AddMinutes(-3));
            await CreateMessageAsync(messageRepo, sessionId, MessageRole.User, "edited user", now.AddMinutes(-2));
            await CreateMessageAsync(messageRepo, sessionId, MessageRole.Assistant, "stale assistant", now.AddMinutes(-1));
        }

        var client = _factory.CreateAuthenticatedClient([ChatbotPermissions.SessionRead], employeeId.ToString());
        var response = await client.DeleteAsync($"/chatbot/v1/internal/sessions/{sessionId}/messages/last-turn");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("removed").GetInt32());

        using var verificationScope = _factory.Services.CreateScope();
        var verificationMessageRepo = verificationScope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var remaining = await verificationMessageRepo.GetMessagesBySessionIdAsync(sessionId);
        Assert.Equal(2, remaining.Count);
        Assert.Equal("first user", remaining[0].Content);
        Assert.Equal("first assistant", remaining[1].Content);
    }

    [Fact]
    public async Task DeleteInternalSessionLastTurn_WhenSessionMissing_ReturnsNotFound()
    {
        var client = _factory.CreateAuthenticatedClient([ChatbotPermissions.SessionRead], Guid.NewGuid().ToString());

        var response = await client.DeleteAsync($"/chatbot/v1/internal/sessions/{Guid.NewGuid()}/messages/last-turn");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task CreateMessageAsync(
        IMessageRepository messageRepo,
        Guid sessionId,
        MessageRole role,
        string content,
        DateTimeOffset createdAt)
    {
        await messageRepo.CreateAsync(new Message
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = role,
            Content = content,
            ContentType = ContentType.Text,
            CreatedAt = createdAt
        });
    }
}
