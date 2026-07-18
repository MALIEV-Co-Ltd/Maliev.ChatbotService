namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Represents a suggested action button in a chatbot response.
/// </summary>
public class SuggestedAction
{
    /// <summary>
    /// Gets or sets the display text for the button.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the label for categorizing the suggested action.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the action to perform when the button is clicked.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data payload for the action.
    /// </summary>
    public string? Data { get; set; }
}
