using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="UserMemory"/> entity operations.
/// </summary>
public interface IUserMemoryRepository
{
    /// <summary>
    /// Creates a new user memory.
    /// </summary>
    /// <param name="userMemory">The user memory to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user memory.</returns>
    Task<UserMemory> CreateAsync(UserMemory userMemory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user memory by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user memory if found; otherwise, null.</returns>
    Task<UserMemory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all memories for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of user memories.</returns>
    Task<List<UserMemory>> GetByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets memories for a user profile with pagination.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of memories list and total count.</returns>
    Task<(List<UserMemory> Memories, int TotalCount)> GetByUserIdAsync(Guid userProfileId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets high confidence memories for a user profile (confidence >= threshold).
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="confidenceThreshold">Minimum confidence threshold (0.0-1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of high confidence user memories.</returns>
    Task<List<UserMemory>> GetHighConfidenceMemoriesAsync(Guid userProfileId, double confidenceThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user memory.
    /// </summary>
    /// <param name="userMemory">The user memory to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(UserMemory userMemory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user memory.
    /// </summary>
    /// <param name="id">The unique identifier of the user memory to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all memories for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user memory by its key.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="key">The memory key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user memory if found; otherwise, null.</returns>
    Task<UserMemory?> GetMemoryByKeyAsync(Guid userProfileId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all memories for a user profile (alias for GetByUserIdAsync).
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of user memories.</returns>
    Task<List<UserMemory>> GetMemoriesByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);
}
