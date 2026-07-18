using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="SearchDomainLog"/> entity operations.
/// </summary>
public interface ISearchDomainLogRepository
{
    /// <summary>
    /// Creates a new search domain log.
    /// </summary>
    /// <param name="searchDomainLog">The search domain log to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created search domain log.</returns>
    Task<SearchDomainLog> CreateAsync(SearchDomainLog searchDomainLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a search domain log by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The search domain log if found; otherwise, null.</returns>
    Task<SearchDomainLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all search domain logs for a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of search domain logs.</returns>
    Task<List<SearchDomainLog>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search domain logs with pagination.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of search domain logs list and total count.</returns>
    Task<(List<SearchDomainLog> Logs, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a search domain log.
    /// </summary>
    /// <param name="id">The unique identifier of the search domain log to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent search domain logs with optional filtering.
    /// </summary>
    /// <param name="count">Number of recent logs to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent search domain logs.</returns>
    Task<List<SearchDomainLog>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default);
}
