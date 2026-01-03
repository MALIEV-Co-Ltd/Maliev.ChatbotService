namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Data transfer object for a knowledge base entry.
/// </summary>
public class KnowledgeBaseDto
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the topic key.
    /// </summary>
    public string TopicKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique fact key.
    /// </summary>
    public string FactKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the factual content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public object? Metadata { get; set; }
}
