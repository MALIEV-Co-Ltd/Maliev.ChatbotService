namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Tracks cumulative model token and estimated cost consumption per user over a rolling 24-hour
/// window and enforces soft daily ceilings (S2). This complements the per-hour message-count rate
/// limit: message count is not a good proxy for cost because a single agent turn can fan out to many
/// model calls with large multimodal payloads. The budget bounds that cost.
/// </summary>
public interface IUsageBudgetService
{
    /// <summary>
    /// Returns true when the user's rolling-24h token or estimated cost usage already meets or exceeds
    /// a configured daily budget. Always false when all budgets are disabled.
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
    /// Adds model tokens and estimated cost to the user's rolling-24h usage and returns the new
    /// running totals. Non-positive values are no-ops for their respective counters.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="usage">The model usage charge to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UsageBudgetRecordResult> RecordModelUsageAsync(
        Guid userProfileId,
        UsageBudgetCharge usage,
        CancellationToken cancellationToken = default);

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
/// Model usage charge to add to a rolling daily budget.
/// </summary>
public sealed class UsageBudgetCharge
{
    /// <summary>Gets or sets the model tokens consumed by the call.</summary>
    public long Tokens { get; set; }

    /// <summary>Gets or sets the estimated model cost in micro-USD, excluding separately tracked Google Search grounding cost.</summary>
    public long CostMicroUsd { get; set; }

    /// <summary>Gets or sets the number of Gemini Google Search grounded prompts consumed by the call.</summary>
    public int GoogleSearchGroundingPromptCount { get; set; }

    /// <summary>Gets or sets the estimated Google Search grounding cost in micro-USD before daily free allowance.</summary>
    public long GoogleSearchGroundingMicroUsd { get; set; }
}

/// <summary>
/// Running rolling daily budget totals after recording a usage charge.
/// </summary>
public sealed class UsageBudgetRecordResult
{
    /// <summary>Gets or sets the number of tokens used in the current rolling window.</summary>
    public long UsedTokens { get; set; }

    /// <summary>Gets or sets the estimated cost used in the current rolling window, in micro-USD.</summary>
    public long UsedCostMicroUsd { get; set; }

    /// <summary>Gets or sets the Google Search grounded prompts covered by the shared daily free allowance.</summary>
    public int FreeGoogleSearchGroundingPromptCount { get; set; }

    /// <summary>Gets or sets the Google Search grounded prompts charged after the shared daily free allowance.</summary>
    public int BillableGoogleSearchGroundingPromptCount { get; set; }

    /// <summary>Gets or sets the Google Search grounding cost charged after the shared daily free allowance.</summary>
    public long ChargedGoogleSearchGroundingMicroUsd { get; set; }
}

/// <summary>
/// Customer-safe rolling daily model budget state.
/// </summary>
public sealed class UsageBudgetSnapshot
{
    /// <summary>Gets or sets whether any daily model budget is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets the number of tokens used in the current rolling window.</summary>
    public long UsedTokens { get; set; }

    /// <summary>Gets or sets the configured daily token budget.</summary>
    public long DailyTokenBudget { get; set; }

    /// <summary>Gets or sets the number of tokens remaining in the current rolling window.</summary>
    public long RemainingTokens { get; set; }

    /// <summary>Gets or sets the used-to-budget ratio from 0 to 1.</summary>
    public double UsedRatio { get; set; }

    /// <summary>Gets or sets the estimated cost used in the current rolling window, in micro-USD.</summary>
    public long UsedCostMicroUsd { get; set; }

    /// <summary>Gets or sets the configured daily cost budget, in micro-USD.</summary>
    public long DailyCostBudgetMicroUsd { get; set; }

    /// <summary>Gets or sets estimated cost remaining in the current rolling window, in micro-USD.</summary>
    public long RemainingCostMicroUsd { get; set; }

    /// <summary>Gets or sets the cost used-to-budget ratio from 0 to 1.</summary>
    public double CostUsedRatio { get; set; }

    /// <summary>Gets or sets whether the user has reached the token budget.</summary>
    public bool IsTokenExceeded { get; set; }

    /// <summary>Gets or sets whether the user has reached the estimated cost budget.</summary>
    public bool IsCostExceeded { get; set; }

    /// <summary>Gets or sets whether the user has reached any configured model budget.</summary>
    public bool IsExceeded { get; set; }
}
