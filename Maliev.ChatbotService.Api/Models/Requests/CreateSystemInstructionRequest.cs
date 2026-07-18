using Maliev.ChatbotService.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request to create a new system instruction.
/// </summary>
public class CreateSystemInstructionRequest
{
    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the instruction (Core, Topic).
    /// </summary>
    [Required]
    public SystemInstructionCategory Category { get; set; } = SystemInstructionCategory.Core;

    /// <summary>
    /// Gets or sets the topic key for specialized instructions.
    /// </summary>
    [StringLength(100)]
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the priority for injection order.
    /// </summary>
    [Range(1, 5)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Gets or sets the persona definition for the chatbot.
    /// </summary>
    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string PersonaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the business constraints and rules.
    /// </summary>
    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string BusinessConstraints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this instruction set should be active.
    /// </summary>
    public bool IsActive { get; set; }
}
