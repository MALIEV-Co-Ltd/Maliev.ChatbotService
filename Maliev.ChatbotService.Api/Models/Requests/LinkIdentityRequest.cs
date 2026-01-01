using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request model for linking an external platform identity to a user profile.
/// </summary>
public class LinkIdentityRequest
{
    /// <summary>
    /// Gets or sets the platform name (e.g., "line", "facebook", "instagram", "whatsapp").
    /// </summary>
    [Required]
    [StringLength(50)]
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external platform user ID.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ExternalUserId { get; set; } = string.Empty;
}
