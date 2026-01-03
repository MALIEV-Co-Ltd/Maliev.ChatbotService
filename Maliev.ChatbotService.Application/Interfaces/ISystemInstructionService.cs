using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for managing system instructions with caching.
/// </summary>
public interface ISystemInstructionService
{
    /// <summary>
    /// Gets the active system instruction with Redis caching.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active system instruction if found; otherwise, null.</returns>
    Task<SystemInstruction?> GetActiveInstructionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the merged system instruction set based on detected topics.
    /// </summary>
    /// <param name="topicKeys">List of detected topic keys.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A merged instruction text containing core and topic-specific instructions.</returns>
    Task<string> GetMergedInstructionsAsync(IEnumerable<string> topicKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cache for system instructions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
