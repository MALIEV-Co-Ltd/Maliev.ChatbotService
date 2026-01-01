namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents the confirmation status of a webhook identity link.
/// </summary>
public enum WebhookConfirmationStatus
{
    /// <summary>
    /// Webhook confirmation is pending.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Webhook confirmation succeeded.
    /// </summary>
    Confirmed = 2,

    /// <summary>
    /// Webhook confirmation failed.
    /// </summary>
    Failed = 3
}
