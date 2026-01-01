using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request model for initiating a new chatbot session.
/// </summary>
public class InitiateSessionRequest
{
    /// <summary>
    /// Gets or sets the channel through which the session is initiated.
    /// </summary>
    [Required]
    [RegularExpression("^(website|line|facebook|instagram|whatsapp|intranet)$",
        ErrorMessage = "Channel must be one of: website, line, facebook, instagram, whatsapp, intranet")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external user ID (for LINE, Facebook, etc.).
    /// </summary>
    public string? ExternalUserId { get; set; }

    /// <summary>
    /// Gets or sets the preferred language for the session (en or th).
    /// </summary>
    [RegularExpression("^(en|th)?$", ErrorMessage = "Language must be 'en' or 'th'")]
    public string? Language { get; set; }
}
