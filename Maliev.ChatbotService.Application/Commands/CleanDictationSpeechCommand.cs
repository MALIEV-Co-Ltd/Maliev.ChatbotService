namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command for cleaning up dictated speech text using Gemini.
/// </summary>
public class CleanDictationSpeechCommand
{
    /// <summary>Gets or sets the raw dictated speech text to clean.</summary>
    public string Speech { get; set; } = string.Empty;

    /// <summary>Gets or sets the speech language code (en or th).</summary>
    public string Language { get; set; } = "en";
}
