namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents persistent facts about a user.
/// </summary>
public class UserMemory
{
    /// <summary>
    /// Gets or sets the unique identifier for the user memory.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID this memory belongs to.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the key for the memory (e.g., "MaterialPreference", "DeliveryAddress").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value in JSON format.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the confidence score for this memory (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the source message ID where this memory was extracted.
    /// </summary>
    public Guid SourceMessageId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this memory was last updated.
    /// </summary>
    public DateTimeOffset LastUpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the user profile navigation property.
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// Gets or sets the source message navigation property.
    /// </summary>
    public Message? SourceMessage { get; set; }
}
