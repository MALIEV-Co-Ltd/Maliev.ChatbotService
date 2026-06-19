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

    /// <summary>
    /// Gets a customer-safe snapshot of the user's rolling-24h token budget state.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UsageBudgetSnapshot> GetDailyTokenUsageSnapshotAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Customer-safe rolling daily token budget state.
/// </summary>
public sealed class UsageBudgetSnapshot
{
    /// <summary>Gets or sets whether the daily token budget is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets the number of tokens used in the current rolling window.</summary>
    public long UsedTokens { get; set; }

    /// <summary>Gets or sets the configured daily token budget.</summary>
    public long DailyTokenBudget { get; set; }

    /// <summary>Gets or sets the number of tokens remaining in the current rolling window.</summary>
    public long RemainingTokens { get; set; }

    /// <summary>Gets or sets the used-to-budget ratio from 0 to 1.</summary>
    public double UsedRatio { get; set; }

    /// <summary>Gets or sets whether the user has reached the budget.</summary>
    public bool IsExceeded { get; set; }
}
