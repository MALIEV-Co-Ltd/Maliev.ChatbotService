namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response model for a chatbot session.
/// </summary>
public class SessionResponse
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the welcome message.
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language code (en or th).
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
