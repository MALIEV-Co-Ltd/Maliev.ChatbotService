namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents the role of a message sender in a conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// Message from the user.
    /// </summary>
    User = 1,

    /// <summary>
    /// Message from the AI assistant.
    /// </summary>
    Assistant = 2,

    /// <summary>
    /// System-generated message.
    /// </summary>
    System = 3
}
