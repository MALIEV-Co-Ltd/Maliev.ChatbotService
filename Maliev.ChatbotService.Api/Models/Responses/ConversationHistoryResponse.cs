namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response containing a page of conversation sessions.
/// </summary>
public class ConversationSessionsResponse
{
    /// <summary>
    /// Gets or sets the conversation session summaries.
    /// </summary>
    public List<ConversationSessionSummaryResponse> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the pagination metadata.
    /// </summary>
    public PaginationMeta Meta { get; set; } = new();
}

/// <summary>
/// Response containing messages for one conversation session.
/// </summary>
public class ConversationMessagesResponse
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID that owns the session.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the session channel.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session start timestamp.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the latest activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// Gets or sets the session expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the language code.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered messages in the session.
    /// </summary>
    public List<ConversationMessageResponse> Messages { get; set; } = [];
}

/// <summary>
/// Summary row for one conversation session.
/// </summary>
public class ConversationSessionSummaryResponse
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the user profile ID that owns the session.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the session channel.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session start timestamp.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the latest activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// Gets or sets the session expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the language code.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first user message preview.
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// Gets or sets the number of messages in the session.
    /// </summary>
    public int MessageCount { get; set; }
}

/// <summary>
/// Message row for a conversation history response.
/// </summary>
public class ConversationMessageResponse
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the sender role.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets customer-safe web grounding provenance for this assistant message.
    /// </summary>
    public GroundingProvenanceResponse? GroundingProvenance { get; set; }
}
