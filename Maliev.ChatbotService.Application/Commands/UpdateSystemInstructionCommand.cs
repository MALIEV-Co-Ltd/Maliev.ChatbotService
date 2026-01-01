namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to update an existing system instruction.
/// </summary>
public class UpdateSystemInstructionCommand
{
    /// <summary>
    /// Gets or sets the unique identifier of the instruction to update.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the persona definition for the chatbot.
    /// </summary>
    public string? PersonaDefinition { get; set; }

    /// <summary>
    /// Gets or sets the business constraints and rules.
    /// </summary>
    public string? BusinessConstraints { get; set; }

    /// <summary>
    /// Gets or sets whether this instruction set should be active.
    /// </summary>
    public bool? IsActive { get; set; }
}
