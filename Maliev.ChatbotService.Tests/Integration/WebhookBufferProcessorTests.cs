using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests the durable webhook processor end-to-end (S6): a buffered message is processed through the
/// chatbot and the buffer is drained. The platform reply clients are mocked (no-op) by the test
/// factory, so a successful reply lets the batch be acknowledged.
/// </summary>
[Collection("Database")]
public class WebhookBufferProcessorTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public WebhookBufferProcessorTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ProcessSessionAsync_BufferedMessage_IsHandledAndBufferDrained()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();

            await userRepo.CreateAsync(new UserProfile
            {
                Id = userId,
                Role = UserRole.Customer,
                CreatedAt = now,
                LastActiveAt = now
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = sessionId,
                UserProfileId = userId,
                Channel = Channel.Line,
                StartTime = now,
                LastActivityAt = now,
                ExpiresAt = now.AddHours(24),
                Language = Language.English,
                Status = SessionStatus.Active
            });
        }

        // Enqueue an inbound webhook message for that session.
        using (var scope = _factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IWebhookBufferQueue>();
            await queue.EnqueueAsync(sessionId, new ProcessWebhookCommand
            {
                Channel = Channel.Line,
                PlatformUserId = "U-test",
                MessageText = "I need pricing for FDM printing",
                ReplyToken = "reply-token",
                Timestamp = now
            });
        }

        // Process it directly (the background poller is disabled under Testing).
        using (var scope = _factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<IWebhookBufferProcessor>();
            await processor.ProcessSessionAsync(sessionId);
        }

        // The turn was handled (an assistant message is persisted) and the buffer is drained.
        using (var scope = _factory.Services.CreateScope())
        {
            var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
            var messages = await messageRepo.GetBySessionIdAsync(sessionId);
            Assert.Contains(messages, m => m.Role == MessageRole.User && m.Content.Contains("FDM printing"));
            Assert.Contains(messages, m => m.Role == MessageRole.Assistant);

            var queue = scope.ServiceProvider.GetRequiredService<IWebhookBufferQueue>();
            Assert.Null(await queue.PeekAsync(sessionId));
        }
    }
}
