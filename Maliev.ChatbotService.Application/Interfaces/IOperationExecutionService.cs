namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for executing CRM operations (quotations, orders, customer updates).
/// </summary>
public interface IOperationExecutionService
{
    /// <summary>
    /// Executes a CRM operation based on the detected intent.
    /// </summary>
    /// <param name="operationType">The type of operation (e.g., "QuotationQuery", "OrderStatusQuery", "CRMUpdate").</param>
    /// <param name="parameters">The operation parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<OperationResult> ExecuteOperationAsync(string operationType, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a CRM operation execution.
/// </summary>
public class OperationResult
{
    /// <summary>
    /// Gets or sets whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the operation data (structured CRM data).
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the error message if unsuccessful.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the suggested actions for the user.
    /// </summary>
    public List<OperationAction> SuggestedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the formatted message for display.
    /// </summary>
    public string? FormattedMessage { get; set; }
}

/// <summary>
/// Represents a suggested action after an operation.
/// </summary>
public class OperationAction
{
    /// <summary>
    /// Gets or sets the action label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action type (e.g., "SendReminder", "UpdateStatus", "ViewDetails").
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action parameters.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}
