using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to process an incoming webhook from a messaging platform.
/// </summary>
public class ProcessWebhookCommand
{
    /// <summary>
    /// Gets or sets the channel that sent the webhook.
    /// </summary>
    public Channel Channel { get; set; }

    /// <summary>
    /// Gets or sets the platform-specific user ID.
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message text from the user.
    /// </summary>
    public string MessageText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reply token (for LINE).
    /// </summary>
    public string? ReplyToken { get; set; }

    /// <summary>
    /// Gets or sets the recipient ID (for Meta platforms).
    /// </summary>
    public string? RecipientId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the webhook event.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
