using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a durable model-provider batch job for conversation summaries.
/// </summary>
public class ConversationSummaryBatchJob
{
    /// <summary>
    /// Gets or sets the unique identifier for the batch job.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the provider batch resource name.
    /// </summary>
    public string BatchName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model used for the batch.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider display name for the batch.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the batch status.
    /// </summary>
    public ConversationSummaryBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the provider error message, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the batch record was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the batch was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the batch was submitted to the provider.
    /// </summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the batch reached a terminal state.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the per-session batch items.
    /// </summary>
    public List<ConversationSummaryBatchItem> Items { get; set; } = [];
}
