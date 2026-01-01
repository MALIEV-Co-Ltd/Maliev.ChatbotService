using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a single message within a conversation session.
/// </summary>
public class Message
{
    /// <summary>
    /// Gets or sets the unique identifier for the message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the session ID this message belongs to.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of content in the message.
    /// </summary>
    public ContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the metadata in JSON format (token usage, processing time, file URLs).
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the conversation session navigation property.
    /// </summary>
    public ConversationSession? Session { get; set; }

    /// <summary>
    /// Gets or sets the collection of operation logs related to this message.
    /// </summary>
    public ICollection<OperationLog> OperationLogs { get; set; } = new List<OperationLog>();
}
