using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Webhooks;

/// <summary>
/// Represents a LINE webhook event payload.
/// </summary>
public class LineWebhookEvent
{
    /// <summary>
    /// Gets or sets the destination (channel ID).
    /// </summary>
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of events.
    /// </summary>
    [JsonPropertyName("events")]
    public List<LineEvent> Events { get; set; } = [];
}

/// <summary>
/// Represents a single LINE event.
/// </summary>
public class LineEvent
{
    /// <summary>
    /// Gets or sets the event type (message, follow, unfollow, etc.).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reply token for responding to this event.
    /// </summary>
    [JsonPropertyName("replyToken")]
    public string ReplyToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source of the event (user, group, room).
    /// </summary>
    [JsonPropertyName("source")]
    public LineSource Source { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the event.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the message object (only for message events).
    /// </summary>
    [JsonPropertyName("message")]
    public LineMessage? Message { get; set; }
}

/// <summary>
/// Represents the source of a LINE event.
/// </summary>
public class LineSource
{
    /// <summary>
    /// Gets or sets the source type (user, group, room).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the group ID (if applicable).
    /// </summary>
    [JsonPropertyName("groupId")]
    public string? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the room ID (if applicable).
    /// </summary>
    [JsonPropertyName("roomId")]
    public string? RoomId { get; set; }
}

/// <summary>
/// Represents a LINE message.
/// </summary>
public class LineMessage
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message type (text, image, video, etc.).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text content (for text messages).
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
