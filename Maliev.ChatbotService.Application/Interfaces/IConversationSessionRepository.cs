using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="ConversationSession"/> entity operations.
/// </summary>
public interface IConversationSessionRepository
{
    /// <summary>
    /// Creates a new conversation session.
    /// </summary>
    /// <param name="session">The conversation session to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation session.</returns>
    Task<ConversationSession> CreateAsync(ConversationSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a conversation session by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation session if found; otherwise, null.</returns>
    Task<ConversationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active sessions for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active conversation sessions.</returns>
    Task<List<ConversationSession>> GetActiveSessionsByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets expired sessions that need to be closed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of expired conversation sessions.</returns>
    Task<List<ConversationSession>> GetExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conversation sessions by user profile ID with pagination.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of sessions list and total count.</returns>
    Task<(List<ConversationSession> Sessions, int TotalCount)> GetByUserIdAsync(Guid userProfileId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a conversation session.
    /// </summary>
    /// <param name="session">The conversation session to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(ConversationSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a conversation session.
    /// </summary>
    /// <param name="id">The unique identifier of the session to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all conversation sessions for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of conversation sessions.</returns>
    Task<List<ConversationSession>> GetSessionsByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active session for a user on a specific channel.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="channelName">The channel name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active conversation session if found; otherwise, null.</returns>
    Task<ConversationSession?> GetActiveSessionByUserAndChannelAsync(Guid userProfileId, PlatformName channelName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a conversation session by setting its end time.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active conversation sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of active sessions.</returns>
    Task<int> GetActiveSessionsCountAsync(CancellationToken cancellationToken = default);
}
