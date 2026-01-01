namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Data transfer object for system instruction.
/// </summary>
public class SystemInstructionDto
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the persona definition for the chatbot.
    /// </summary>
    public string PersonaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the business constraints and rules.
    /// </summary>
    public string BusinessConstraints { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this instruction set is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int Version { get; set; }
}
