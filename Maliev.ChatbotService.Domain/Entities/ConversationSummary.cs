namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents long-term memory of a closed conversation session.
/// </summary>
public class ConversationSummary
{
    /// <summary>
    /// Gets or sets the unique identifier for the conversation summary.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the session ID that was summarized.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID associated with this summary.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the structured summary in JSON format containing topics, decisions, preferences, entities, intent categories, and unresolved questions.
    /// </summary>
    public string StructuredSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the summary was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user profile navigation property.
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// Gets or sets the conversation session navigation property.
    /// </summary>
    public ConversationSession? Session { get; set; }
}
