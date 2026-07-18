namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents the status of a conversation session.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Session is currently active.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Session has been closed.
    /// </summary>
    Closed = 2
}
