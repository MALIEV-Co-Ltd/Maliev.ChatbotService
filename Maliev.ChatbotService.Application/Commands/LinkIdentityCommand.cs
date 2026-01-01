namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command for linking an external platform identity to a user profile.
/// </summary>
public class LinkIdentityCommand
{
    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the platform name.
    /// </summary>
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external platform user ID.
    /// </summary>
    public string ExternalUserId { get; set; } = string.Empty;
}
