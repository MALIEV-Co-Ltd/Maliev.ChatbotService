using System.ComponentModel.DataAnnotations;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request to improve a system instruction draft with the configured LLM.
/// </summary>
public class RefineSystemInstructionRequest
{
    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the instruction.
    /// </summary>
    [Required]
    public SystemInstructionCategory Category { get; set; } = SystemInstructionCategory.Core;

    /// <summary>
    /// Gets or sets the topic key for specialized instructions.
    /// </summary>
    [StringLength(100)]
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the persona definition or skill prompt body to improve.
    /// </summary>
    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string PersonaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the business constraints and guardrails to improve.
    /// </summary>
    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string BusinessConstraints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional operator guidance for the refinement.
    /// </summary>
    [StringLength(500)]
    public string? ImprovementGoal { get; set; }
}
