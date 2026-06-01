using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Request model for sending a message in a chatbot session.
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    [Required]
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    [Required]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 4000 characters")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the caller-selected response language, either en or th.
    /// </summary>
    [RegularExpression("^(en|th)?$", ErrorMessage = "Language must be 'en' or 'th'")]
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the list of attachments for the message.
    /// </summary>
    public List<Attachment>? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the response MIME type for structured output (e.g., "application/json").
    /// </summary>
    public string? ResponseMimeType { get; set; }

    /// <summary>
    /// Gets or sets the JSON Schema for structured output.
    /// </summary>
    public object? ResponseSchema { get; set; }

    /// <summary>
    /// Gets or sets the callback URL for streaming thinking steps.
    /// When provided, thinking steps are POSTed to this URL as they happen.
    /// </summary>
    public string? CallbackUrl { get; set; }
}
