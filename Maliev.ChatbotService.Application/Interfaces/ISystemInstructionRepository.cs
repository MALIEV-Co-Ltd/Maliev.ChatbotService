using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="SystemInstruction"/> entity operations.
/// </summary>
public interface ISystemInstructionRepository
{
    /// <summary>
    /// Creates a new system instruction.
    /// </summary>
    /// <param name="instruction">The system instruction to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created system instruction.</returns>
    Task<SystemInstruction> CreateAsync(SystemInstruction instruction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a system instruction by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The system instruction if found; otherwise, null.</returns>
    Task<SystemInstruction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active core system instruction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active core system instruction if found; otherwise, null.</returns>
    Task<SystemInstruction?> GetActiveCoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active topic-specific system instructions for the given topic keys.
    /// </summary>
    /// <param name="topicKeys">The topic keys to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of active topic-specific system instructions.</returns>
    Task<List<SystemInstruction>> GetActiveByTopicsAsync(IEnumerable<string> topicKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active system instruction.
    /// </summary>
    /// <param name="category">Optional category filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active system instruction if found; otherwise, null.</returns>
    Task<SystemInstruction?> GetActiveAsync(SystemInstructionCategory? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all system instructions with pagination and filtering.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="topicKey">Optional topic key filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of instructions list and total count.</returns>
    Task<(List<SystemInstruction> Instructions, int TotalCount)> GetAllAsync(int page, int pageSize, SystemInstructionCategory? category = null, string? topicKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a system instruction.
    /// </summary>
    /// <param name="instruction">The system instruction to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(SystemInstruction instruction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates all system instructions for a specific category or topic.
    /// </summary>
    /// <param name="category">The category to deactivate.</param>
    /// <param name="topicKey">Optional topic key to deactivate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeactivateAllAsync(SystemInstructionCategory? category = null, string? topicKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a system instruction.
    /// </summary>
    /// <param name="id">The unique identifier of the instruction to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active system instruction (alias for GetActiveAsync).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active system instruction if found; otherwise, null.</returns>
    Task<SystemInstruction?> GetActiveInstructionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a system instruction and deactivates all others.
    /// </summary>
    /// <param name="id">The unique identifier of the instruction to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a system instruction by version number.
    /// </summary>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The system instruction if found; otherwise, null.</returns>
    Task<SystemInstruction?> GetByVersionAsync(int version, CancellationToken cancellationToken = default);
}
