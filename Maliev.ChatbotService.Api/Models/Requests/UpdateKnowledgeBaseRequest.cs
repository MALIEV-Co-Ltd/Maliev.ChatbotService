using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request to update an existing knowledge base entry.
/// </summary>
public class UpdateKnowledgeBaseRequest
{
    /// <summary>
    /// Gets or sets the topic key linking this fact to a specific domain.
    /// </summary>
    [StringLength(100)]
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the unique key for this specific fact within the topic.
    /// </summary>
    [StringLength(200)]
    public string? FactKey { get; set; }

    /// <summary>
    /// Gets or sets the factual content.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this entry.
    /// </summary>
    public object? Metadata { get; set; }
}
