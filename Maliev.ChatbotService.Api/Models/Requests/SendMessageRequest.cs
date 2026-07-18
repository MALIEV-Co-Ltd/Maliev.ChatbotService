using System.ComponentModel.DataAnnotations;
using Maliev.ChatbotService.Application.Handlers;

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
    [StringLength(MessagePipelinePolicy.MaxContentCharacters, MinimumLength = 1, ErrorMessage = "Content must be between {2} and {1} characters")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the caller-selected response language, either en or th.
    /// </summary>
    [RegularExpression("^(en|th)?$", ErrorMessage = "Language must be 'en' or 'th'")]
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets an optional model override for narrow utility calls.
    /// </summary>
    [StringLength(80)]
    public string? ModelName { get; set; }

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

    /// <summary>
    /// Gets or sets the signed QuoteEngine agent context token forwarded to QuoteEngine tools.
    /// </summary>
    [StringLength(4096)]
    public string? QuoteAgentContextToken { get; set; }
}
