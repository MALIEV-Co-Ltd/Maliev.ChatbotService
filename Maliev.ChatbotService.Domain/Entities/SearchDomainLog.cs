namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a log entry for domains accessed during web searches.
/// </summary>
public class SearchDomainLog
{
    /// <summary>
    /// Gets or sets the unique identifier for the search domain log.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the session ID where the search occurred.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the domain that was accessed.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the search query that triggered the domain access.
    /// </summary>
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the domain was accessed.
    /// </summary>
    public DateTimeOffset AccessedAt { get; set; }

    /// <summary>
    /// Gets or sets the conversation session navigation property.
    /// </summary>
    public ConversationSession? Session { get; set; }
}
