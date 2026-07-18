namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command for extracting customer intent from a user message.
/// </summary>
public class ExtractCustomerIntentCommand
{
    /// <summary>
    /// Gets or sets the user message to analyze.
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;
}
