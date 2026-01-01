namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for rate limiting operations.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if the user has exceeded the rate limit.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if rate limit is exceeded; otherwise, false.</returns>
    Task<bool> IsRateLimitExceededAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the message count for a user.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current message count.</returns>
    Task<int> IncrementMessageCountAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the remaining messages for a user before hitting the rate limit.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of remaining messages.</returns>
    Task<int> GetRemainingMessagesAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the user is within rate limits and returns the limit status.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple indicating if rate limit is exceeded and remaining count.</returns>
    Task<(bool IsExceeded, int Remaining)> CheckRateLimitAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the request count for a user.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IncrementCountAsync(Guid userProfileId, CancellationToken cancellationToken = default);
}
