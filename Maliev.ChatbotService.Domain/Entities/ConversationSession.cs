using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a 24-hour conversation window.
/// </summary>
public class ConversationSession
{
    /// <summary>
    /// Gets or sets the unique identifier for the conversation session.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID associated with this session.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the channel used for this session.
    /// </summary>
    public Channel Channel { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session started.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last activity in this session.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session expires (calculated as LastActivityAt + 24 hours).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the language used in this session.
    /// </summary>
    public Language Language { get; set; }

    /// <summary>
    /// Gets or sets the status of the session.
    /// </summary>
    public SessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the conversation summary ID if the session has been summarized.
    /// </summary>
    public Guid? SummaryId { get; set; }

    /// <summary>
    /// Gets or sets the user profile navigation property.
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// Gets or sets the collection of messages in this session.
    /// </summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();

    /// <summary>
    /// Gets or sets the conversation summary navigation property.
    /// </summary>
    public ConversationSummary? Summary { get; set; }
}
