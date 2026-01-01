namespace Maliev.ChatbotService.Domain.Events;

/// <summary>
/// Event published when a user exceeds the chatbot rate limit.
/// </summary>
public class ChatbotRateLimitExceededEvent
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the source service.
    /// </summary>
    public string Source { get; set; } = "ChatbotService";

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current message count.
    /// </summary>
    public int CurrentMessageCount { get; set; }

    /// <summary>
    /// Gets or sets the rate limit threshold.
    /// </summary>
    public int RateLimitThreshold { get; set; }

    /// <summary>
    /// Gets or sets the time when rate limit resets.
    /// </summary>
    public DateTimeOffset ResetAt { get; set; }
}
