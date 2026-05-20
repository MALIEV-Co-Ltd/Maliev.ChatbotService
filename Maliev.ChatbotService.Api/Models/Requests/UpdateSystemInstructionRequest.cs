using Maliev.ChatbotService.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request to update an existing system instruction.
/// </summary>
public class UpdateSystemInstructionRequest
{
    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the category of the instruction (Core, Topic).
    /// </summary>
    public SystemInstructionCategory? Category { get; set; }

    /// <summary>
    /// Gets or sets the topic key for specialized instructions.
    /// </summary>
    [StringLength(100)]
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the priority for injection order.
    /// </summary>
    [Range(1, 5)]
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets the persona definition for the chatbot.
    /// </summary>
    [StringLength(5000, MinimumLength = 10)]
    public string? PersonaDefinition { get; set; }

    /// <summary>
    /// Gets or sets the business constraints and rules.
    /// </summary>
    [StringLength(5000, MinimumLength = 10)]
    public string? BusinessConstraints { get; set; }

    /// <summary>
    /// Gets or sets whether this instruction set should be active.
    /// </summary>
    public bool? IsActive { get; set; }
}
