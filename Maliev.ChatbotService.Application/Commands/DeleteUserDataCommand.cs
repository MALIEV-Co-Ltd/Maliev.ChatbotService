namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to delete user data with specified scope.
/// </summary>
public class DeleteUserDataCommand
{
    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the scope of deletion (preferences, history, all).
    /// </summary>
    public string Scope { get; set; } = "all";

    /// <summary>
    /// Gets or sets whether the deletion is confirmed.
    /// </summary>
    public bool Confirm { get; set; }
}
