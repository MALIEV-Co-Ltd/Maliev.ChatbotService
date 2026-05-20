using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to improve a system instruction draft using the configured LLM.
/// </summary>
public class RefineSystemInstructionCommand
{
    /// <summary>
    /// Gets or sets the display name of the instruction being refined.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the instruction being refined.
    /// </summary>
    public SystemInstructionCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the profile or topic key of the instruction being refined.
    /// </summary>
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the persona or skill prompt body to improve.
    /// </summary>
    public string PersonaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the business constraints and guardrails to improve.
    /// </summary>
    public string BusinessConstraints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional operator guidance for the refinement.
    /// </summary>
    public string? ImprovementGoal { get; set; }
}

/// <summary>
/// Result produced by LLM-assisted system instruction refinement.
/// </summary>
public class RefinedSystemInstructionResult
{
    /// <summary>
    /// Gets or sets the improved persona or skill prompt body.
    /// </summary>
    public string PersonaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the improved business constraints and guardrails.
    /// </summary>
    public string BusinessConstraints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a concise summary of the improvements made.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}
