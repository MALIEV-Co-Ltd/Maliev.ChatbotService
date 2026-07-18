namespace Maliev.ChatbotService.Domain.Enums;

/// <summary>
/// Represents the type of content in a message.
/// </summary>
public enum ContentType
{
    /// <summary>
    /// Text content.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Image content.
    /// </summary>
    Image = 2,

    /// <summary>
    /// Audio content.
    /// </summary>
    Audio = 3,

    /// <summary>
    /// PDF document content.
    /// </summary>
    PDF = 4,

    /// <summary>
    /// Video content.
    /// </summary>
    Video = 5
}
