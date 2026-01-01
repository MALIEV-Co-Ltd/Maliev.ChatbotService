namespace Maliev.ChatbotService.Domain.Events;

/// <summary>
/// Event published when a chatbot session is closed or expired.
/// </summary>
public class ChatbotSessionClosedEvent
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
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the session end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the total message count.
    /// </summary>
    public int TotalMessageCount { get; set; }

    /// <summary>
    /// Gets or sets the closure reason.
    /// </summary>
    public string ClosureReason { get; set; } = string.Empty;
}
