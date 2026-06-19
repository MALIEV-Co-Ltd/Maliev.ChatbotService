using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="Message"/> entity operations.
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Creates a new message.
    /// </summary>
    /// <param name="message">The message to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created message.</returns>
    Task<Message> CreateAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a message by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message if found; otherwise, null.</returns>
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages for a conversation session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of messages ordered by creation time.</returns>
    Task<List<Message>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent messages for a conversation session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="count">Number of recent messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent messages ordered by creation time.</returns>
    Task<List<Message>> GetRecentBySessionIdAsync(Guid sessionId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a message.
    /// </summary>
    /// <param name="message">The message to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a message.
    /// </summary>
    /// <param name="id">The unique identifier of the message to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages for a conversation session (alias for GetBySessionIdAsync).
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of messages ordered by creation time.</returns>
    Task<List<Message>> GetMessagesBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the most recent customer turn for a session: the last user message and every message
    /// that followed it (assistant reply, tool messages). Used to roll back the last turn so an edited
    /// message can be resubmitted as a clean correction.
    /// </summary>
    /// <param name="sessionId">The conversation session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of messages removed.</returns>
    Task<int> DeleteLastTurnAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
