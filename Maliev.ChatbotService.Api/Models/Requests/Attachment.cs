using System.ComponentModel.DataAnnotations;

namespace Maliev.ChatbotService.Api.Models.Requests;

/// <summary>
/// Represents a file attachment in a chatbot message.
/// </summary>
public class Attachment
{
    /// <summary>
    /// Gets or sets the attachment type (image, document, etc.).
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file URL or base64-encoded data.
    /// </summary>
    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the attachment.
    /// </summary>
    [StringLength(100)]
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the filename of the attachment.
    /// </summary>
    [StringLength(255)]
    public string? Filename { get; set; }

    /// <summary>
    /// Gets or sets the size of the attachment in bytes.
    /// </summary>
    public long? SizeBytes { get; set; }
}
