namespace Maliev.ChatbotService.Domain.Events;

/// <summary>
/// Event published when a chatbot message is received and processed.
/// </summary>
public class ChatbotMessageReceivedEvent
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
    /// Gets or sets the language.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user message content.
    /// </summary>
    public string UserMessageContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assistant response content.
    /// </summary>
    public string AssistantResponseContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response latency in milliseconds.
    /// </summary>
    public double ResponseLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the message received time.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; }
}
