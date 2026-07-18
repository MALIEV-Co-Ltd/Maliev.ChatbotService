namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response model for the clean dictation speech endpoint.
/// </summary>
public class CleanDictationSpeechResponse
{
    /// <summary>Gets or sets the cleaned speech text.</summary>
    public string CleanedText { get; set; } = string.Empty;
}
