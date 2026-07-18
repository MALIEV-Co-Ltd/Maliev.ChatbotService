namespace Maliev.ChatbotService.Application.Models;

/// <summary>
/// Represents a user preference extracted from a conversation.
/// </summary>
public class ExtractedPreference
{
    /// <summary>
    /// Gets or sets the preference key (e.g., "MaterialPreference", "DeliveryAddress").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preference value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this preference was last updated.
    /// </summary>
    public DateTimeOffset? LastUpdated { get; set; }
}
