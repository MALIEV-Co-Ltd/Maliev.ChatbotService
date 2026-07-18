namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents the role of a user in the chatbot system.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// External customer user.
    /// </summary>
    Customer = 1,

    /// <summary>
    /// Internal agent user with CRM access.
    /// </summary>
    InternalAgent = 2
}
