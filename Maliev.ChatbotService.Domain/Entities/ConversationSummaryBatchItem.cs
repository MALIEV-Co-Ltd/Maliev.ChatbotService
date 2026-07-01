using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents one conversation session inside a summary batch job.
/// </summary>
public class ConversationSummaryBatchItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the batch item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning batch job ID.
    /// </summary>
    public Guid BatchJobId { get; set; }

    /// <summary>
    /// Gets or sets the session ID being summarized.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID associated with the session.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the item status.
    /// </summary>
    public ConversationSummaryBatchStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the generated structured summary JSON, if available.
    /// </summary>
    public string? StructuredSummary { get; set; }

    /// <summary>
    /// Gets or sets the provider error message, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets token usage metadata JSON, if available.
    /// </summary>
    public string? TokenUsageJson { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the item record was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the item was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the item reached a terminal state.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the owning batch job.
    /// </summary>
    public ConversationSummaryBatchJob? BatchJob { get; set; }

    /// <summary>
    /// Gets or sets the conversation session.
    /// </summary>
    public ConversationSession? Session { get; set; }

    /// <summary>
    /// Gets or sets the user profile.
    /// </summary>
    public UserProfile? UserProfile { get; set; }
}
