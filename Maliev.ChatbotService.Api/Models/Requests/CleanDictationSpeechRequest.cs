using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request model for cleaning up raw dictated speech text.
/// </summary>
public class CleanDictationSpeechRequest
{
    /// <summary>Gets or sets the raw dictated speech text to clean up.</summary>
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Speech { get; set; } = string.Empty;

    /// <summary>Gets or sets the speech language code (en or th).</summary>
    public string Language { get; set; } = "en";
}
