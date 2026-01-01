namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Client interface for IAM Service operations.
/// </summary>
public interface IIAMServiceClient
{
    /// <summary>
    /// Checks if a user has a specific permission.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="permission">The permission to check (e.g., "chatbot.instructions.read").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has the permission; otherwise, false.</returns>
    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permissions for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of permissions.</returns>
    Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
}
