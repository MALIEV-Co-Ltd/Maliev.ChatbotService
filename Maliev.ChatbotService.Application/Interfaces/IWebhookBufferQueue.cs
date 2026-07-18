using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// A snapshot of the buffered messages and reply context for one session, peeked from the durable
/// queue without removing anything (S6). The buffer is only removed on a successful
/// <see cref="IWebhookBufferQueue.AcknowledgeAsync"/>.
/// </summary>
/// <param name="SessionId">The conversation session the buffered messages belong to.</param>
/// <param name="CombinedContent">The buffered message texts joined into a single turn.</param>
/// <param name="MessageCount">How many buffered messages this snapshot covers (the count to acknowledge).</param>
/// <param name="Channel">The platform channel to reply on.</param>
/// <param name="ReplyToken">The LINE reply token, if any.</param>
/// <param name="RecipientId">The Meta recipient ID, if any.</param>
/// <param name="PlatformUserId">The platform-specific user ID (kept for a future push-API reply path).</param>
public sealed record BufferedWebhookBatch(
    Guid SessionId,
    string CombinedContent,
    int MessageCount,
    Channel Channel,
    string? ReplyToken,
    string? RecipientId,
    string PlatformUserId);

/// <summary>
/// Durable, restart-safe buffer for debounced inbound webhook messages (S6). Replaces the previous
/// fire-and-forget <c>Task.Run</c> debounce — all state lives in Redis, so a process crash between
/// receiving a message and processing it no longer drops the work: the lease on the in-flight session
/// expires and the next poll reclaims it (at-least-once).
/// </summary>
public interface IWebhookBufferQueue
{
    /// <summary>
    /// Appends an inbound message to its session buffer, records the latest reply context, and schedules
    /// the session to be processed after the debounce window. Re-scheduling on each message extends the
    /// debounce so a burst of messages is processed as one turn.
    /// </summary>
    Task EnqueueAsync(Guid sessionId, ProcessWebhookCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims all sessions due at or before <paramref name="asOf"/> and leases each forward
    /// (visibility timeout) so a concurrent or subsequent claim will not return it until the lease
    /// expires. Sessions that have exceeded the processing-attempt cap are dropped (and not returned).
    /// </summary>
    Task<IReadOnlyList<Guid>> ClaimDueSessionsAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads (without removing) the buffered batch and reply context for a session. Returns null when
    /// the buffer is empty.
    /// </summary>
    Task<BufferedWebhookBatch?> PeekAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks <paramref name="processedCount"/> messages as successfully handled, removing exactly that
    /// many from the head of the buffer (messages that arrived during processing survive). If the buffer
    /// is then empty the session is removed from the schedule; otherwise it is rescheduled after the
    /// debounce window for the remainder.
    /// </summary>
    Task AcknowledgeAsync(Guid sessionId, int processedCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a processing failure and reschedules the session for a near-term retry (the buffer is kept
    /// intact). The attempt cap that ultimately drops a poison batch is enforced at claim time.
    /// </summary>
    Task ReportFailureAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
