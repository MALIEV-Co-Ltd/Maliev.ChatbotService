namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents system actions and API calls performed by the chatbot.
/// </summary>
public class OperationLog
{
    /// <summary>
    /// Gets or sets the unique identifier for the operation log.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID associated with this operation.
    /// </summary>
    public Guid? UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the message ID that initiated this operation.
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Gets or sets the type of operation (e.g., "QuotationCreated", "OrderStatusQueried", "CRMUpdated").
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input parameters for the operation in JSON format.
    /// </summary>
    public string? OperationParameters { get; set; }

    /// <summary>
    /// Gets or sets the output result or error details in JSON format.
    /// </summary>
    public string? ExecutionResult { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the operation was executed.
    /// </summary>
    public DateTimeOffset ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the source of the action. Values: "user" (direct), "ai-assistant" (chatbot-initiated).
    /// </summary>
    public string ActionSource { get; set; } = "user";

    /// <summary>
    /// Gets or sets the user profile navigation property.
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// Gets or sets the message navigation property.
    /// </summary>
    public Message? Message { get; set; }
}
