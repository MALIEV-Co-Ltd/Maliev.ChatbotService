namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Tracks cumulative model token consumption per user over a rolling 24-hour window and enforces a
/// soft daily ceiling (S2). This complements the per-hour message-count rate limit: message count is
/// not a good proxy for cost because a single agent turn can fan out to many model calls with large
/// multimodal payloads. The budget bounds that cost.
/// </summary>
public interface IUsageBudgetService
{
    /// <summary>
    /// Returns true when the user's rolling-24h token usage already meets or exceeds the configured
    /// daily budget. Always false when the budget is disabled (configured value &lt;= 0).
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsDailyTokenBudgetExceededAsync(Guid userProfileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the given token count to the user's rolling-24h usage and returns the new running total.
    /// A non-positive token count is a no-op (returns the current total without mutating the window).
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="tokens">The number of tokens consumed by the turn.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> RecordTokenUsageAsync(Guid userProfileId, long tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's current rolling-24h token usage.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> GetDailyTokenUsageAsync(Guid userProfileId, CancellationToken cancellationToken = default);
}
