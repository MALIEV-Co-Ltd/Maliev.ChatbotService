using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service for validating file attachments in chatbot messages.
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    /// Validates an attachment based on type and size constraints.
    /// </summary>
    /// <param name="mimeType">The MIME type of the file.</param>
    /// <param name="sizeBytes">The size of the file in bytes.</param>
    /// <param name="contentType">The content type category.</param>
    /// <returns>A tuple indicating if the file is valid and an error message if not.</returns>
    (bool IsValid, string? ErrorMessage) ValidateAttachment(string mimeType, long sizeBytes, ContentType contentType);

    /// <summary>
    /// Determines the content type from a MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type.</param>
    /// <returns>The content type category.</returns>
    ContentType GetContentTypeFromMimeType(string mimeType);
}
