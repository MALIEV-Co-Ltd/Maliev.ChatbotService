using Maliev.ChatbotService.Application.Models;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to send a message in a chatbot session.
/// </summary>
public class SendMessageCommand
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the caller-selected response language, either en or th.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the list of attachments for multimodal messages.
    /// </summary>
    public List<AttachmentDto>? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the response MIME type for structured output (e.g., "application/json").
    /// </summary>
    public string? ResponseMimeType { get; set; }

    /// <summary>
    /// Gets or sets the JSON Schema for structured output.
    /// </summary>
    public object? ResponseSchema { get; set; }

    /// <summary>
    /// Optional callback for streaming thinking steps to the caller.
    /// </summary>
    public Func<ThinkingStep, Task>? ThinkingStepCallback { get; set; }

    /// <summary>
    /// Optional callback for streaming generated assistant text deltas to the caller.
    /// </summary>
    public Func<string, Task>? TextDeltaCallback { get; set; }

    /// <summary>
    /// The Bearer token of the authenticated user, forwarded to downstream tool calls.
    /// </summary>
    public string? UserToken { get; set; }

    /// <summary>
    /// Signed QuoteEngine agent context forwarded to QuoteEngine BFF tool endpoints.
    /// </summary>
    public string? QuoteAgentContextToken { get; set; }
}

/// <summary>
/// DTO representing a message attachment.
/// </summary>
public class AttachmentDto
{
    /// <summary>
    /// Gets or sets the content type (Image, PDF, Video, Audio).
    /// </summary>
    public ContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the data URL or base64-encoded data.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }
}
