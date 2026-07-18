using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for the durable, restart-safe webhook buffer (S6). Drive the queue directly against real
/// Redis; the claim lease is deterministic via the <c>asOf</c> parameter so the crash-recovery
/// behaviour is testable with no real waits. Defaults: debounce 2s, lease 360s, max attempts 3.
/// </summary>
[Collection("Database")]
public class WebhookBufferQueueTests : IAsyncLifetime
{
    private const int LeaseSeconds = 360;
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public WebhookBufferQueueTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private IWebhookBufferQueue Queue(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IWebhookBufferQueue>();

    private static ProcessWebhookCommand LineMessage(string text, string replyToken = "reply-token") => new()
    {
        Channel = Channel.Line,
        PlatformUserId = "U-test",
        MessageText = text,
        ReplyToken = replyToken,
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ClaimDueSessions_LeasesClaimedSession_AndReclaimsAfterLeaseExpires()
    {
        // The acceptance test for S6: once claimed, a session is invisible until its lease expires; a
        // crash (no ack) is recovered when a later poll reclaims it.
        using var scope = _factory.Services.CreateScope();
        var queue = Queue(scope);
        var sessionId = Guid.NewGuid();

        await queue.EnqueueAsync(sessionId, LineMessage("hello"));

        var asOf = DateTimeOffset.UtcNow.AddSeconds(5); // safely past the 2s debounce

        var firstClaim = await queue.ClaimDueSessionsAsync(asOf);
        Assert.Contains(sessionId, firstClaim);

        var secondClaim = await queue.ClaimDueSessionsAsync(asOf);
        Assert.DoesNotContain(sessionId, secondClaim); // leased — invisible

        var afterLease = await queue.ClaimDueSessionsAsync(asOf.AddSeconds(LeaseSeconds + 1));
        Assert.Contains(sessionId, afterLease); // lease expired — recovered
    }

    [Fact]
    public async Task Enqueue_DuringLease_DoesNotReclaim_PreservingSingleReply()
    {
        // A message arriving while a turn is being processed must not pull the leased session due again
        // (which would re-claim it and deliver a duplicate reply). The remainder is processed only after
        // the in-flight worker acknowledges.
        using var scope = _factory.Services.CreateScope();
        var queue = Queue(scope);
        var sessionId = Guid.NewGuid();

        await queue.EnqueueAsync(sessionId, LineMessage("hello"));

        var asOf = DateTimeOffset.UtcNow.AddSeconds(5);
        var firstClaim = await queue.ClaimDueSessionsAsync(asOf);
        Assert.Contains(sessionId, firstClaim); // leased to asOf + 360s

        // Second message arrives "during processing".
        await queue.EnqueueAsync(sessionId, LineMessage("you there?"));

        // Still leased — must not be re-claimed by the next poll.
        var midClaim = await queue.ClaimDueSessionsAsync(asOf.AddSeconds(1));
        Assert.DoesNotContain(sessionId, midClaim);

        // The worker processed only the first message; the remainder is rescheduled by the ack.
        await queue.AcknowledgeAsync(sessionId, 1);
        var afterAck = await queue.ClaimDueSessionsAsync(DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Contains(sessionId, afterAck);

        var remainder = await queue.PeekAsync(sessionId);
        Assert.Equal("you there?", remainder!.CombinedContent);
    }

    [Fact]
    public async Task Peek_ReturnsCombinedContentAndLatestContext_WithoutRemoving()
    {
        using var scope = _factory.Services.CreateScope();
        var queue = Queue(scope);
        var sessionId = Guid.NewGuid();

        await queue.EnqueueAsync(sessionId, LineMessage("hello", "token-1"));
        await queue.EnqueueAsync(sessionId, LineMessage("world", "token-2"));

        var batch = await queue.PeekAsync(sessionId);

        Assert.NotNull(batch);
        Assert.Equal("hello\nworld", batch!.CombinedContent);
        Assert.Equal(2, batch.MessageCount);
        Assert.Equal(Channel.Line, batch.Channel);
        Assert.Equal("token-2", batch.ReplyToken); // latest context wins
        Assert.Equal("U-test", batch.PlatformUserId);

        // Peeking does not consume the buffer.
        var again = await queue.PeekAsync(sessionId);
        Assert.Equal(2, again!.MessageCount);
    }

    [Fact]
    public async Task Acknowledge_AllMessages_DrainsBufferAndClearsSchedule()
    {
        using var scope = _factory.Services.CreateScope();
        var queue = Queue(scope);
        var sessionId = Guid.NewGuid();

        await queue.EnqueueAsync(sessionId, LineMessage("hello"));
        await queue.EnqueueAsync(sessionId, LineMessage("world"));

        await queue.AcknowledgeAsync(sessionId, 2);

        Assert.Null(await queue.PeekAsync(sessionId));
        // Removed from the schedule entirely.
        var claim = await queue.ClaimDueSessionsAsync(DateTimeOffset.UtcNow.AddSeconds(LeaseSeconds + 10));
        Assert.DoesNotContain(sessionId, claim);
    }

    [Fact]
    public async Task Acknowledge_PartialBatch_KeepsRemainderAndReschedules()
    {
        using var scope = _factory.Services.CreateScope();
        var queue = Queue(scope);
        var sessionId = Guid.NewGuid();

        await queue.EnqueueAsync(sessionId, LineMessage("hello"));
        await queue.EnqueueAsync(sessionId, LineMessage("world"));

        // Acknowledge only the first message (e.g. another arrived during processing).
        await queue.AcknowledgeAsync(sessionId, 1);

        var batch = await queue.PeekAsync(sessionId);
        Assert.NotNull(batch);
        Assert.Equal("world", batch!.CombinedContent);
        Assert.Equal(1, batch.MessageCount);

        var claim = await queue.ClaimDueSessionsAsync(DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Contains(sessionId, claim); // rescheduled for the remainder
    }

    [Fact]
    public async Task ClaimDueSessions_DropsBatch_AfterExceedingAttemptCap()
    {
        using var scope = _factory.Services.CreateScope();
        var queue = Queue(scope);
        var sessionId = Guid.NewGuid();

        await queue.EnqueueAsync(sessionId, LineMessage("poison"));

        var asOf = DateTimeOffset.UtcNow.AddSeconds(5);

        // Each claim counts as an attempt; with maxAttempts=3 the 4th claim drops the poison batch.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var claim = await queue.ClaimDueSessionsAsync(asOf.AddSeconds(attempt * (LeaseSeconds + 1)));
            Assert.Contains(sessionId, claim);
        }

        var dropClaim = await queue.ClaimDueSessionsAsync(asOf.AddSeconds(3 * (LeaseSeconds + 1)));
        Assert.DoesNotContain(sessionId, dropClaim);
        Assert.Null(await queue.PeekAsync(sessionId)); // buffer dropped
    }
}
