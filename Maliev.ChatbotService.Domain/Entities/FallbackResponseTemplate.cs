using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a fallback response template for graceful degradation.
/// </summary>
public class FallbackResponseTemplate
{
    /// <summary>
    /// Gets or sets the unique identifier for the fallback template.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the scenario type (e.g., "GeminiAPITimeout", "GeminiAPIError", "RedisUnavailable", "ValidationFailure", "UnexpectedError").
    /// </summary>
    public string ScenarioType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language for this template.
    /// </summary>
    public Language Language { get; set; }

    /// <summary>
    /// Gets or sets the fallback response text.
    /// </summary>
    public string ResponseText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this template includes contact information.
    /// </summary>
    public bool IncludesContactInfo { get; set; }

    /// <summary>
    /// Gets or sets the priority order (lower number = higher priority).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets whether this template is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this template was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this template was last updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
