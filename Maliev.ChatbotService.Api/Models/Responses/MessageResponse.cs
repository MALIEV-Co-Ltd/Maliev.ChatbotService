namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response model for a chatbot message.
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of the message sender (user or assistant).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language code (en or th).
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of suggested actions/buttons.
    /// </summary>
    public List<SuggestedAction> SuggestedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
