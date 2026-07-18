using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for conversation summary operations.
/// </summary>
public interface IConversationSummaryService
{
    /// <summary>
    /// Generates a structured summary for a conversation session.
    /// </summary>
    /// <param name="sessionId">The session ID to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation summary.</returns>
    Task<ConversationSummary> GenerateSummaryAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent conversation summaries for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="limit">The maximum number of summaries to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection of conversation summaries.</returns>
    Task<IEnumerable<ConversationSummary>> GetRecentSummariesAsync(Guid userProfileId, int limit = 3, CancellationToken cancellationToken = default);
}
