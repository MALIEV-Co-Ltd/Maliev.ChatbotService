namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response model for a chatbot message.
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role of the message sender (user or assistant).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language code (en or th).
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of suggested actions/buttons.
    /// </summary>
    public List<SuggestedAction> SuggestedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the thinking steps from AI agent processing.
    /// </summary>
    public List<ThinkingStepResponse> ThinkingSteps { get; set; } = new();

    /// <summary>
    /// Gets or sets the current daily token usage snapshot.
    /// </summary>
    public UsageSnapshotResponse? UsageSnapshot { get; set; }
}

/// <summary>
/// Customer-safe daily token usage snapshot.
/// </summary>
public class UsageSnapshotResponse
{
    /// <summary>Gets or sets whether daily token budgeting is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets tokens used in the rolling daily window.</summary>
    public long UsedTokens { get; set; }

    /// <summary>Gets or sets the configured daily token budget.</summary>
    public long DailyTokenBudget { get; set; }

    /// <summary>Gets or sets tokens remaining in the rolling daily window.</summary>
    public long RemainingTokens { get; set; }

    /// <summary>Gets or sets the usage ratio from 0 to 1.</summary>
    public double UsedRatio { get; set; }

    /// <summary>Gets or sets estimated cost used in the rolling daily window, in micro-USD.</summary>
    public long UsedCostMicroUsd { get; set; }

    /// <summary>Gets or sets the configured daily estimated cost budget, in micro-USD.</summary>
    public long DailyCostBudgetMicroUsd { get; set; }

    /// <summary>Gets or sets estimated cost remaining in the rolling daily window, in micro-USD.</summary>
    public long RemainingCostMicroUsd { get; set; }

    /// <summary>Gets or sets the estimated cost usage ratio from 0 to 1.</summary>
    public double CostUsedRatio { get; set; }

    /// <summary>Gets or sets whether the token budget has been reached.</summary>
    public bool IsTokenExceeded { get; set; }

    /// <summary>Gets or sets whether the estimated cost budget has been reached.</summary>
    public bool IsCostExceeded { get; set; }

    /// <summary>Gets or sets whether any configured budget has been reached.</summary>
    public bool IsExceeded { get; set; }
}

/// <summary>
/// Stream event emitted while processing a chatbot message.
/// </summary>
public class MessageStreamEvent
{
    /// <summary>
    /// Gets or sets the event type: started, delta, thought, final, or error.
    /// </summary>
    public string Type { get; set; } = "delta";

    /// <summary>
    /// Gets or sets the incremental assistant text for delta events.
    /// </summary>
    public string? Delta { get; set; }

    /// <summary>
    /// Gets or sets incremental thought text for thought-type events.
    /// </summary>
    public string? Thought { get; set; }

    /// <summary>
    /// Gets or sets the final persisted assistant message for final events.
    /// </summary>
    public MessageResponse? Message { get; set; }

    /// <summary>
    /// Gets or sets a customer-safe error message for error events.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// A step in the AI's thinking chain.
/// </summary>
public class ThinkingStepResponse
{
    /// <summary>The order of this thinking step.</summary>
    public int StepNumber { get; set; }
    /// <summary>The category of thinking (e.g., Search, Analysis, Action).</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Short summary title of the step.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Detailed description or content of the step.</summary>
    public string Detail { get; set; } = string.Empty;
    /// <summary>When the step started.</summary>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>How long the step took in milliseconds.</summary>
    public long? DurationMs { get; set; }
}
