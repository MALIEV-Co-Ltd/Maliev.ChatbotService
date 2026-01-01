using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for conversation summary operations.
/// </summary>
public interface IConversationSummaryRepository
{
    /// <summary>
    /// Creates a new conversation summary.
    /// </summary>
    /// <param name="summary">The conversation summary to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation summary.</returns>
    Task<ConversationSummary> CreateAsync(ConversationSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a conversation summary by session ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation summary if found; otherwise, null.</returns>
    Task<ConversationSummary?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent conversation summaries for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="limit">The maximum number of summaries to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection of conversation summaries.</returns>
    Task<IEnumerable<ConversationSummary>> GetRecentByUserProfileIdAsync(Guid userProfileId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing conversation summary.
    /// </summary>
    /// <param name="summary">The conversation summary to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated conversation summary.</returns>
    Task<ConversationSummary> UpdateAsync(ConversationSummary summary, CancellationToken cancellationToken = default);
}
