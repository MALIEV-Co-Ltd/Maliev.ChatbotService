using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for batching non-urgent conversation summary generation.
/// </summary>
public interface IConversationSummaryBatchService
{
    /// <summary>
    /// Submits eligible expired sessions for asynchronous summary generation.
    /// </summary>
    /// <param name="sessions">Expired sessions to evaluate for batch submission.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session IDs that are already queued or were successfully submitted for batch processing.</returns>
    Task<HashSet<Guid>> SubmitExpiredSessionSummariesAsync(
        IReadOnlyCollection<ConversationSession> sessions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls open batch jobs and persists completed summary results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessOpenBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes one provider batch job by its resource name and persists completed summary results.
    /// </summary>
    /// <param name="batchName">The provider batch resource name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessBatchAsync(
        string batchName,
        CancellationToken cancellationToken = default);
}
