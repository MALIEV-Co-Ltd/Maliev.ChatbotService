using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents the association between external platform IDs and internal user profile.
/// </summary>
public class IdentityLink
{
    /// <summary>
    /// Gets or sets the unique identifier for the identity link.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID this link belongs to.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the platform name.
    /// </summary>
    public PlatformName PlatformName { get; set; }

    /// <summary>
    /// Gets or sets the external platform ID.
    /// </summary>
    public string ExternalPlatformId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the webhook confirmation status.
    /// </summary>
    public WebhookConfirmationStatus WebhookConfirmationStatus { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the link was created.
    /// </summary>
    public DateTimeOffset LinkCreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the link was last verified.
    /// </summary>
    public DateTimeOffset LastVerifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the user profile navigation property.
    /// </summary>
    public UserProfile? UserProfile { get; set; }
}
