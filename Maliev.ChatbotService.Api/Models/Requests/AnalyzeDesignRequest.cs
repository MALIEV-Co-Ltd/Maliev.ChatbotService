using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>Request payload for analyzing design complexity from reference images.</summary>
public class AnalyzeDesignRequest
{
    /// <summary>URLs of customer-provided reference images or technical drawings.</summary>
    [Required]
    [JsonPropertyName("imageUrls")]
    public required List<string> ImageUrls { get; init; }

    /// <summary>Customer's text description of the design requirements.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Whether 2D technical drawings are also requested as deliverables.</summary>
    [JsonPropertyName("requiresDrawings")]
    public bool RequiresDrawings { get; init; }
}
