using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="IdentityLink"/> entity operations.
/// </summary>
public interface IIdentityLinkRepository
{
    /// <summary>
    /// Creates a new identity link.
    /// </summary>
    /// <param name="identityLink">The identity link to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created identity link.</returns>
    Task<IdentityLink> CreateAsync(IdentityLink identityLink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an identity link by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identity link if found; otherwise, null.</returns>
    Task<IdentityLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an identity link by platform and external platform ID.
    /// </summary>
    /// <param name="platformName">The platform name.</param>
    /// <param name="externalPlatformId">The external platform ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identity link if found; otherwise, null.</returns>
    Task<IdentityLink?> GetByPlatformIdAsync(PlatformName platformName, string externalPlatformId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all identity links for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of identity links.</returns>
    Task<List<IdentityLink>> GetByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an identity link.
    /// </summary>
    /// <param name="identityLink">The identity link to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(IdentityLink identityLink, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an identity link.
    /// </summary>
    /// <param name="id">The unique identifier of the identity link to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all identity links for a user profile (alias for GetByUserIdAsync).
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of identity links.</returns>
    Task<List<IdentityLink>> GetLinksByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a link between a user profile and a platform.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="platformName">The platform name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveLinkAsync(Guid userProfileId, PlatformName platformName, CancellationToken cancellationToken = default);
}
