namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for processing expired chatbot sessions.
/// </summary>
public interface ISessionExpiryService
{
    /// <summary>
    /// Processes all expired sessions and marks them as closed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
