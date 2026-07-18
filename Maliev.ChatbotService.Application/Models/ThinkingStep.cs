namespace Maliev.ChatbotService.Application.Models;

/// <summary>
/// Represents a step in the AI's thinking/reasoning chain during function calling.
/// </summary>
public class ThinkingStep
{
    /// <summary>Step number in the sequence.</summary>
    public int StepNumber { get; set; }

    /// <summary>Type of step: "function_call", "function_result", or "reasoning".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Human-readable title (e.g., "Searching for customer...").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of this step.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>When this step occurred.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Duration of this step in milliseconds (for function results).</summary>
    public long? DurationMs { get; set; }

    /// <summary>Optional structured result data.</summary>
    public object? Data { get; set; }
}
