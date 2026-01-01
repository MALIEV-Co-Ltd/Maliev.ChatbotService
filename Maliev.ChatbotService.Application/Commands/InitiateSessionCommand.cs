namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to initiate a new chatbot session.
/// </summary>
public class InitiateSessionCommand
{
    /// <summary>
    /// Gets or sets the channel through which the session is initiated.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external user ID (for LINE, Facebook, etc.).
    /// </summary>
    public string? ExternalUserId { get; set; }

    /// <summary>
    /// Gets or sets the preferred language for the session (en or th).
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the authenticated user profile ID (if user is authenticated).
    /// </summary>
    public Guid? AuthenticatedUserProfileId { get; set; }
}
