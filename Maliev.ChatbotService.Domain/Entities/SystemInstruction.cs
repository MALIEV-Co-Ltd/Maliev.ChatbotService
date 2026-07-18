using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents configurable persona and rules for the chatbot.
/// </summary>
public class SystemInstruction
{
    /// <summary>
    /// Gets or sets the unique identifier for the system instruction.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the instruction set.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the instruction (Core, Topic).
    /// </summary>
    public SystemInstructionCategory Category { get; set; } = SystemInstructionCategory.Core;

    /// <summary>
    /// Gets or sets the topic key for specialized instructions. Null for core persona.
    /// </summary>
    public string? TopicKey { get; set; }

    /// <summary>
    /// Gets or sets the priority for injection order when multiple topics match.
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
    /// Gets or sets the allowed topics for the chatbot (comma-separated).
    /// </summary>
    public string AllowedTopics { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rejection templates in JSON format for off-topic requests.
    /// </summary>
    public string RejectionTemplates { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this instruction set is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the version number of this instruction set.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets whether web search capabilities are enabled for this instruction set.
    /// </summary>
    public bool EnableWebSearch { get; set; }

    /// <summary>
    /// Gets or sets whether to log domains accessed during web searches.
    /// </summary>
    public bool LogSearchDomains { get; set; } = true;
}
