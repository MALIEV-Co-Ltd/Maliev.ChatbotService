using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository for durable conversation summary batch jobs.
/// </summary>
public interface IConversationSummaryBatchJobRepository
{
    /// <summary>
    /// Creates a new conversation summary batch job.
    /// </summary>
    /// <param name="job">The batch job to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created batch job.</returns>
    Task<ConversationSummaryBatchJob> CreateAsync(
        ConversationSummaryBatchJob job,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a batch job by provider batch resource name.
    /// </summary>
    /// <param name="batchName">The provider batch resource name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch job, if found.</returns>
    Task<ConversationSummaryBatchJob?> GetByBatchNameAsync(
        string batchName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets non-terminal batch jobs that still need provider polling or submission.
    /// </summary>
    /// <param name="limit">The maximum number of jobs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The open batch jobs.</returns>
    Task<List<ConversationSummaryBatchJob>> GetOpenJobsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the session already has non-terminal batch summary work.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when open work exists.</returns>
    Task<bool> HasOpenItemForSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing conversation summary batch job.
    /// </summary>
    /// <param name="job">The batch job to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(
        ConversationSummaryBatchJob job,
        CancellationToken cancellationToken = default);
}
