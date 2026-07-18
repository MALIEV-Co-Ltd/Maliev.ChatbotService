using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="OperationLog"/> entity operations.
/// </summary>
public interface IOperationLogRepository
{
    /// <summary>
    /// Creates a new operation log.
    /// </summary>
    /// <param name="operationLog">The operation log to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created operation log.</returns>
    Task<OperationLog> CreateAsync(OperationLog operationLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an operation log by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation log if found; otherwise, null.</returns>
    Task<OperationLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all operation logs for a user profile with pagination.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of operation logs list and total count.</returns>
    Task<(List<OperationLog> Logs, int TotalCount)> GetByUserIdAsync(Guid userProfileId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all operation logs for a message.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of operation logs.</returns>
    Task<List<OperationLog>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an operation log.
    /// </summary>
    /// <param name="operationLog">The operation log to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(OperationLog operationLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an operation log.
    /// </summary>
    /// <param name="id">The unique identifier of the operation log to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all operation logs for a user profile without pagination.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of operation logs.</returns>
    Task<List<OperationLog>> GetLogsByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets operation logs by operation type.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of operation logs.</returns>
    Task<List<OperationLog>> GetLogsByOperationTypeAsync(string operationType, CancellationToken cancellationToken = default);
}
