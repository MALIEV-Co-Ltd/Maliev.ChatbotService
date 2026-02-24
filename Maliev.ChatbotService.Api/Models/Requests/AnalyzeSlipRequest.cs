using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request for bank slip analysis.
/// </summary>
public class AnalyzeSlipRequest
{
    /// <summary>
    /// The URL of the slip image to analyze.
    /// </summary>
    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;
}
