namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a granular fact or piece of knowledge for specialized domains.
/// </summary>
public class KnowledgeBase
{
    /// <summary>
    /// Gets or sets the unique identifier for the knowledge base entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the topic key linking this fact to a specific domain.
    /// </summary>
    public string TopicKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique key for this specific fact within the topic.
    /// </summary>
    public string FactKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the factual content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata in JSON format.
    /// </summary>
    public string Metadata { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
