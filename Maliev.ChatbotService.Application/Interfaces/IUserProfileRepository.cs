using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="UserProfile"/> entity operations.
/// </summary>
public interface IUserProfileRepository
{
    /// <summary>
    /// Creates a new user profile.
    /// </summary>
    /// <param name="userProfile">The user profile to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user profile.</returns>
    Task<UserProfile> CreateAsync(UserProfile userProfile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile if found; otherwise, null.</returns>
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user profile by LINE user ID.
    /// </summary>
    /// <param name="lineUserId">The LINE user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile if found; otherwise, null.</returns>
    Task<UserProfile?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user profile by Facebook user ID.
    /// </summary>
    /// <param name="facebookId">The Facebook user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile if found; otherwise, null.</returns>
    Task<UserProfile?> GetByFacebookIdAsync(string facebookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user profile by Instagram user ID.
    /// </summary>
    /// <param name="instagramId">The Instagram user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile if found; otherwise, null.</returns>
    Task<UserProfile?> GetByInstagramIdAsync(string instagramId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user profile by WhatsApp user ID.
    /// </summary>
    /// <param name="whatsAppId">The WhatsApp user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile if found; otherwise, null.</returns>
    Task<UserProfile?> GetByWhatsAppIdAsync(string whatsAppId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user profile by internal user ID.
    /// </summary>
    /// <param name="internalUserId">The internal user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile if found; otherwise, null.</returns>
    Task<UserProfile?> GetByInternalUserIdAsync(string internalUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user profile.
    /// </summary>
    /// <param name="userProfile">The user profile to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(UserProfile userProfile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user profile.
    /// </summary>
    /// <param name="id">The unique identifier of the user profile to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
