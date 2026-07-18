namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response containing an LLM-improved system instruction draft.
/// </summary>
public class RefinedSystemInstructionResponse
{
    /// <summary>
    /// Gets or sets the improved persona definition or skill prompt body.
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
