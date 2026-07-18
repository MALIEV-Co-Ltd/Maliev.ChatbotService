namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Processes one due session from the durable webhook buffer (S6): peeks the buffered batch, runs it
/// through the chatbot, replies on the originating platform, and acknowledges (or reports failure).
/// Resolved in a fresh DI scope per session by the polling background service.
/// </summary>
public interface IWebhookBufferProcessor
{
    /// <summary>
    /// Processes the buffered messages for a single session. Safe to call when the buffer is empty
    /// (no-op). On a handler exception the batch is left buffered and rescheduled for retry.
    /// </summary>
    Task ProcessSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
