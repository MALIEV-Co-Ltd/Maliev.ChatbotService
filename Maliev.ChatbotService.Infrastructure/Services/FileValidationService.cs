using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service implementation for file validation with size and type constraints.
/// </summary>
public class FileValidationService : IFileValidationService
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10MB
    private const long MaxPdfSizeBytes = 20 * 1024 * 1024; // 20MB
    private const long MaxVideoSizeBytes = 50 * 1024 * 1024; // 50MB
    private const long MaxAudioSizeBytes = 10 * 1024 * 1024; // 10MB

    private readonly ILogger<FileValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileValidationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FileValidationService(ILogger<FileValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates an attachment based on type and size constraints.
    /// </summary>
    /// <param name="mimeType">The MIME type of the file.</param>
    /// <param name="sizeBytes">The size of the file in bytes.</param>
    /// <param name="contentType">The content type category.</param>
    /// <returns>A tuple indicating if the file is valid and an error message if not.</returns>
    public (bool IsValid, string? ErrorMessage) ValidateAttachment(string mimeType, long sizeBytes, ContentType contentType)
    {
        // Validate MIME type matches content type
        var expectedContentType = GetContentTypeFromMimeType(mimeType);
        if (expectedContentType != contentType)
        {
            var message = $"MIME type '{mimeType}' does not match declared content type '{contentType}'";
            _logger.LogWarning("File validation failed: {Message}", message);
            return (false, message);
        }

        // Validate size based on content type
        var maxSize = contentType switch
        {
            ContentType.Image => MaxImageSizeBytes,
            ContentType.PDF => MaxPdfSizeBytes,
            ContentType.Video => MaxVideoSizeBytes,
            ContentType.Audio => MaxAudioSizeBytes,
            _ => 0L
        };

        if (sizeBytes > maxSize)
        {
            var maxSizeMB = maxSize / (1024.0 * 1024.0);
            var actualSizeMB = sizeBytes / (1024.0 * 1024.0);
            var message = $"{contentType} file size {actualSizeMB:F2}MB exceeds maximum allowed size of {maxSizeMB:F0}MB";
            _logger.LogWarning("File validation failed: {Message}", message);
            return (false, message);
        }

        _logger.LogDebug("File validation passed: {ContentType}, {SizeMB}MB", contentType, sizeBytes / (1024.0 * 1024.0));
        return (true, null);
    }

    /// <summary>
    /// Determines the content type from a MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type.</param>
    /// <returns>The content type category.</returns>
    public ContentType GetContentTypeFromMimeType(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
        {
            return ContentType.Text;
        }

        var mimeTypeLower = mimeType.ToLowerInvariant();

        if (mimeTypeLower.StartsWith("image/"))
        {
            return ContentType.Image;
        }

        if (mimeTypeLower == "application/pdf")
        {
            return ContentType.PDF;
        }

        if (mimeTypeLower.StartsWith("video/"))
        {
            return ContentType.Video;
        }

        if (mimeTypeLower.StartsWith("audio/"))
        {
            return ContentType.Audio;
        }

        return ContentType.Text;
    }
}
