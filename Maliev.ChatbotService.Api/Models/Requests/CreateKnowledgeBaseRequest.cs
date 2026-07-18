using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request to create a new knowledge base entry.
/// </summary>
public class CreateKnowledgeBaseRequest
{
    /// <summary>
    /// Gets or sets the topic key linking this fact to a specific domain.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string TopicKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique key for this specific fact within the topic.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string FactKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the factual content.
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata for this entry.
    /// </summary>
    public object? Metadata { get; set; }
}
