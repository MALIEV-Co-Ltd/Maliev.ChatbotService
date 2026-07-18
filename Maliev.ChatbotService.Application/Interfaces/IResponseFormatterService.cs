using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for formatting chatbot responses with suggested actions.
/// </summary>
public interface IResponseFormatterService
{
    /// <summary>
    /// Formats a response with suggested action buttons based on the content and language.
    /// </summary>
    /// <param name="content">The response content.</param>
    /// <param name="language">The language of the response.</param>
    /// <returns>A tuple containing the formatted content and list of suggested actions.</returns>
    (string FormattedContent, List<SuggestedActionDto> SuggestedActions) FormatResponse(string content, Language language);

    /// <summary>
    /// Gets a welcome message in the specified language.
    /// </summary>
    /// <param name="language">The language for the welcome message.</param>
    /// <returns>The welcome message.</returns>
    string GetWelcomeMessage(Language language);
}

/// <summary>
/// Data transfer object for suggested actions.
/// </summary>
public class SuggestedActionDto
{
    /// <summary>
    /// Gets or sets the display text for the button.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data payload for the action.
    /// </summary>
    public string? Data { get; set; }
}
