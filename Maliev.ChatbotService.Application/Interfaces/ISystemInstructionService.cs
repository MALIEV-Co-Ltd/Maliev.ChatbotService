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
    /// Invalidates the cache for system instructions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
