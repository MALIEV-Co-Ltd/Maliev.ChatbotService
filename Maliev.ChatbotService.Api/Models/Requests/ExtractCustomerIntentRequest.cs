using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request model for extracting customer intent from a user message.
/// </summary>
public class ExtractCustomerIntentRequest
{
    /// <summary>
    /// Gets or sets the user message to analyze.
    /// </summary>
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string UserMessage { get; set; } = string.Empty;
}
