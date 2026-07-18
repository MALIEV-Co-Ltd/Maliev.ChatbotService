namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Queues verified Gemini Batch API webhook notifications for asynchronous processing.
/// </summary>
public interface IGeminiBatchWebhookQueue
{
    /// <summary>
    /// Enqueues a verified Gemini batch webhook notification.
    /// </summary>
    /// <param name="notification">The verified webhook notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask EnqueueAsync(
        GeminiBatchWebhookNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next Gemini batch webhook notification.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next queued notification.</returns>
    ValueTask<GeminiBatchWebhookNotification> DequeueAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Verified Gemini Batch API webhook notification.
/// </summary>
public sealed class GeminiBatchWebhookNotification
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiBatchWebhookNotification"/> class.
    /// </summary>
    /// <param name="webhookId">The Standard Webhooks message ID.</param>
    /// <param name="eventType">The Gemini webhook event type.</param>
    /// <param name="batchName">The Gemini batch job resource name, when supplied.</param>
    public GeminiBatchWebhookNotification(string webhookId, string eventType, string? batchName)
    {
        WebhookId = webhookId;
        EventType = eventType;
        BatchName = batchName;
    }

    /// <summary>
    /// Gets the Standard Webhooks message ID.
    /// </summary>
    public string WebhookId { get; }

    /// <summary>
    /// Gets the Gemini webhook event type.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Gets the Gemini batch job resource name, when supplied.
    /// </summary>
    public string? BatchName { get; }
}
