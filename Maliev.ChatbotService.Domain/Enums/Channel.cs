namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents the communication channel used for a conversation session.
/// </summary>
public enum Channel
{
    /// <summary>
    /// Website channel.
    /// </summary>
    Website = 1,

    /// <summary>
    /// LINE messaging platform.
    /// </summary>
    Line = 2,

    /// <summary>
    /// Facebook Messenger.
    /// </summary>
    Facebook = 3,

    /// <summary>
    /// Instagram messaging.
    /// </summary>
    Instagram = 4,

    /// <summary>
    /// WhatsApp messaging.
    /// </summary>
    WhatsApp = 5,

    /// <summary>
    /// Internal intranet channel.
    /// </summary>
    Intranet = 6
}
