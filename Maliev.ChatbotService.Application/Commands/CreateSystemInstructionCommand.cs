using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to create a new system instruction.
/// </summary>
public class CreateSystemInstructionCommand
{
    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the instruction.
    /// </summary>
    public SystemInstructionCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the topic key for specialized instructions.
    /// </summary>
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the priority for injection order.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the persona definition for the chatbot.
    /// </summary>
    public string PersonaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the business constraints and rules.
    /// </summary>
    public string BusinessConstraints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this instruction set should be active.
    /// </summary>
    public bool IsActive { get; set; }
}
