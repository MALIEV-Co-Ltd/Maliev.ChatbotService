using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Result of bank slip analysis.
/// </summary>
public class SlipAnalysisResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the slip is valid.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Gets or sets the extracted amount in THB.
    /// </summary>
    [JsonPropertyName("extractedAmountThb")]
    public decimal? ExtractedAmountThb { get; set; }
    
    /// <summary>
    /// Gets or sets the bank name identified from the slip.
    /// </summary>
    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }
    
    /// <summary>
    /// Gets or sets the transfer date extracted from the slip.
    /// </summary>
    [JsonPropertyName("transferDate")]
    public string? TransferDate { get; set; }
    
    /// <summary>
    /// Gets or sets additional notes or error messages from the analysis.
    /// </summary>
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}
