namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents lifecycle state for a conversation summary batch job or item.
/// </summary>
public enum ConversationSummaryBatchStatus
{
    /// <summary>
    /// The batch work is prepared but has not been submitted.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The batch work has been submitted to the model provider.
    /// </summary>
    Submitted = 1,

    /// <summary>
    /// The batch work completed successfully.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// The batch work failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The batch work was cancelled.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// The batch work expired before completion.
    /// </summary>
    Expired = 5
}
