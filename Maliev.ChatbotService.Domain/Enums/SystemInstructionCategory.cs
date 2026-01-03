namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Categorization for system instructions to distinguish between core persona and topic-specific knowledge.
/// </summary>
public enum SystemInstructionCategory
{
    /// <summary>
    /// Core persona and safety instructions, always included in every request.
    /// </summary>
    Core = 1,

    /// <summary>
    /// Specialized domain knowledge or topic-specific rules, injected based on detected user intent.
    /// </summary>
    Topic = 2
}
