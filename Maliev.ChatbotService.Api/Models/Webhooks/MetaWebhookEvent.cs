using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Webhooks;

/// <summary>
/// Represents a Meta webhook event payload (Facebook, Instagram, WhatsApp).
/// </summary>
public class MetaWebhookEvent
{
    /// <summary>
    /// Gets or sets the object type (page, instagram, whatsapp_business_account).
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of entries.
    /// </summary>
    [JsonPropertyName("entry")]
    public List<MetaEntry> Entry { get; set; } = [];
}

/// <summary>
/// Represents a Meta webhook entry.
/// </summary>
public class MetaEntry
{
    /// <summary>
    /// Gets or sets the page or app ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>
    /// Gets or sets the messaging events (for Messenger).
    /// </summary>
    [JsonPropertyName("messaging")]
    public List<MetaMessaging>? Messaging { get; set; }

    /// <summary>
    /// Gets or sets the changes array (for Instagram/WhatsApp).
    /// </summary>
    [JsonPropertyName("changes")]
    public List<MetaChange>? Changes { get; set; }
}

/// <summary>
/// Represents a Meta messaging event.
/// </summary>
public class MetaMessaging
{
    /// <summary>
    /// Gets or sets the sender information.
    /// </summary>
    [JsonPropertyName("sender")]
    public MetaUser Sender { get; set; } = new();

    /// <summary>
    /// Gets or sets the recipient information.
    /// </summary>
    [JsonPropertyName("recipient")]
    public MetaUser Recipient { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the message object.
    /// </summary>
    [JsonPropertyName("message")]
    public MetaMessage? Message { get; set; }
}

/// <summary>
/// Represents a Meta user.
/// </summary>
public class MetaUser
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Represents a Meta message.
/// </summary>
public class MetaMessage
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    [JsonPropertyName("mid")]
    public string Mid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Represents a Meta change event (Instagram/WhatsApp).
/// </summary>
public class MetaChange
{
    /// <summary>
    /// Gets or sets the field that changed.
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value object.
    /// </summary>
    [JsonPropertyName("value")]
    public MetaChangeValue Value { get; set; } = new();
}

/// <summary>
/// Represents the value of a Meta change.
/// </summary>
public class MetaChangeValue
{
    /// <summary>
    /// Gets or sets the messaging events.
    /// </summary>
    [JsonPropertyName("messaging")]
    public List<MetaMessaging>? Messaging { get; set; }

    /// <summary>
    /// Gets or sets the messages (WhatsApp).
    /// </summary>
    [JsonPropertyName("messages")]
    public List<MetaMessage>? Messages { get; set; }
}
