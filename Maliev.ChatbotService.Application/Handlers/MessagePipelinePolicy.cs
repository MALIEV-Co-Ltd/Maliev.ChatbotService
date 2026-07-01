using System.Text.Json;
using System.Text.Json.Serialization;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Pure, channel-aware policy helpers for the message pipeline: customer-input validation (S3)
/// and channel-scoped domain topic injection (P1). Extracted from
/// <see cref="SendMessageCommandHandler"/> so the rules are unit-testable without standing up the
/// full handler and its dependencies.
/// </summary>
public static class MessagePipelinePolicy
{
    /// <summary>Maximum accepted length of a single customer message, in characters.</summary>
    public const int MaxContentCharacters = 8000;

    private static readonly HashSet<string> GeminiExternalUrlMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html",
        "text/css",
        "text/plain",
        "text/xml",
        "text/csv",
        "text/rtf",
        "text/javascript",
        "application/json",
        "application/pdf",
        "image/bmp",
        "image/jpeg",
        "image/png",
        "image/webp",
        "video/mp4",
        "video/mpeg",
        "video/quicktime",
        "video/avi",
        "video/x-flv",
        "video/mpg",
        "video/webm",
        "video/wmv",
        "video/3gpp"
    };

    private const double TopicConfidenceThreshold = 0.7;

    /// <summary>
    /// Normalizes free-form chat content (strips null bytes) and rejects abusive lengths.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT apply SQL/keyword "injection" heuristics: those reject ordinary
    /// manufacturing language such as "create a quote", "select PLA", or "where is my order" and are
    /// unsuitable for natural-language chat. Empty content is allowed because a message may carry only
    /// image/PDF/CAD attachments.
    /// </remarks>
    /// <param name="content">Raw customer content.</param>
    /// <param name="normalized">Normalized content when the method returns true; empty otherwise.</param>
    /// <param name="error">A customer-safe error message when the method returns false.</param>
    /// <returns>True when the content is acceptable; otherwise false.</returns>
    public static bool TryNormalizeContent(string? content, out string normalized, out string? error)
    {
        normalized = (content ?? string.Empty).Replace("\0", string.Empty);

        if (normalized.Length > MaxContentCharacters)
        {
            error = $"Message is too long ({normalized.Length} characters). " +
                $"Please shorten it to {MaxContentCharacters} characters or fewer.";
            normalized = string.Empty;
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Indicates whether the channel may receive intranet domain topic prompts and knowledge-base
    /// facts. Only the internal intranet channel may; customer-facing channels (Website, QuoteEngine,
    /// and social channels) must not, so internal ERP/CRM instructions and facts never leak (P1).
    /// </summary>
    public static bool AllowsDomainTopicInjection(Channel channel) => channel == Channel.Intranet;

    /// <summary>
    /// Builds the set of topic keys to inject for the given channel and classification result.
    /// Returns an empty list for customer-facing channels so internal topics never leak into a
    /// customer conversation.
    /// </summary>
    public static List<string> BuildInjectableTopicKeys(Channel channel, IntentClassificationResult classification)
    {
        var topicKeys = new List<string>();

        if (classification is null || !AllowsDomainTopicInjection(channel))
        {
            return topicKeys;
        }

        if (!string.IsNullOrEmpty(classification.Intent) &&
            !string.Equals(classification.Intent, "General", StringComparison.OrdinalIgnoreCase) &&
            classification.Confidence > TopicConfidenceThreshold)
        {
            topicKeys.Add(classification.Intent);
        }

        if (classification.AdditionalTopics != null)
        {
            topicKeys.AddRange(classification.AdditionalTopics);
        }

        return topicKeys;
    }

    /// <summary>Maximum number of attachments accepted on a single message.</summary>
    public const int MaxAttachmentsPerMessage = 8;

    /// <summary>Maximum combined attachment size accepted on a single message, in bytes.</summary>
    public const long MaxTotalAttachmentBytes = 75L * 1024 * 1024;

    /// <summary>
    /// Caps the number and combined size of attachments on one message to bound per-message vision
    /// cost (S2). Per-attachment type limits are enforced separately by the caller.
    /// </summary>
    public static bool TryValidateAttachmentBudget(int count, long totalBytes, out string? error)
    {
        if (count > MaxAttachmentsPerMessage)
        {
            error = $"Too many attachments ({count}). Please send at most {MaxAttachmentsPerMessage} files per message.";
            return false;
        }

        if (totalBytes > MaxTotalAttachmentBytes)
        {
            error = $"Attachments are too large in total ({totalBytes / (1024 * 1024)}MB). " +
                $"Please keep the combined size under {MaxTotalAttachmentBytes / (1024 * 1024)}MB per message.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates a new model-fetched attachment reference before Gemini processing. URL/GCS/File API
    /// references must carry a caller-supplied size so per-message cost limits are meaningful.
    /// </summary>
    public static bool TryValidateGeminiAttachmentReference(
        string? data,
        string? mimeType,
        long sizeBytes,
        out string? error)
    {
        if (!IsModelFetchedAttachmentReference(data))
        {
            error = null;
            return true;
        }

        if (data!.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            error = "URL-based attachments must use HTTPS, a Gemini File API URI, or gs:// storage reference.";
            return false;
        }

        if (sizeBytes <= 0)
        {
            error = "Attachment size must be provided for URL-based files before Gemini processing.";
            return false;
        }

        if (IsExternalHttpsUrl(data) && !IsSupportedGeminiExternalUrlMimeType(mimeType))
        {
            error = $"Unsupported external attachment MIME type '{mimeType}'. " +
                "Use a supported text, JSON, PDF, image, or video MIME type.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Indicates whether a public or signed HTTPS file URL MIME type is in the Gemini documented
    /// external URL allowlist for generateContent fileData.
    /// </summary>
    public static bool IsSupportedGeminiExternalUrlMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        return GeminiExternalUrlMimeTypes.Contains(mimeType.Trim());
    }

    /// <summary>
    /// Serializes URL/GCS-style attachment references for persistence on a message so later turns can
    /// re-hydrate them (C3). Inline base64 data is intentionally skipped to avoid bloating the store.
    /// Returns null when there is nothing persistable.
    /// </summary>
    public static string? BuildAttachmentMetadataJson(IReadOnlyList<(string MimeType, string Data)>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return null;
        }

        var persistable = attachments
            .Where(a => IsPersistableReference(a.Data))
            .Select(a => new PersistedAttachment { Mime = a.MimeType, Data = a.Data })
            .ToList();

        return persistable.Count == 0
            ? null
            : JsonSerializer.Serialize(new AttachmentMetadata { Attachments = persistable });
    }

    /// <summary>
    /// Re-hydrates attachment references previously persisted by <see cref="BuildAttachmentMetadataJson"/>.
    /// Returns an empty list for null/invalid metadata or metadata without attachment references.
    /// </summary>
    public static List<GeminiAttachment> ParsePersistedAttachments(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            var meta = JsonSerializer.Deserialize<AttachmentMetadata>(metadataJson);
            if (meta?.Attachments is null)
            {
                return [];
            }

            return meta.Attachments
                .Where(a => !string.IsNullOrWhiteSpace(a.Data))
                .Select(a => new GeminiAttachment
                {
                    ContentType = a.Mime ?? string.Empty,
                    MimeType = a.Mime ?? string.Empty,
                    Data = a.Data!
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Builds the customer-safe, localized message returned when a user has reached the daily token
    /// budget (S2). A soft cap: the turn is refused gracefully in-band rather than with a hard error,
    /// and points the customer at the MALIEV team to continue.
    /// </summary>
    public static string BuildDailyBudgetExceededMessage(Language language) => language == Language.Thai
        ? "คุณใช้งานผู้ช่วยนี้ครบตามขีดจำกัดการใช้งานของวันนี้แล้ว กรุณาลองใหม่อีกครั้งในภายหลัง " +
          "หรือติดต่อทีมงาน MALIEV ที่ info@maliev.com เพื่อดำเนินการต่อ"
        : "You've reached today's usage limit for this assistant. Please try again later, " +
          "or contact the MALIEV team at info@maliev.com to continue.";

    private static bool IsPersistableReference(string? data) =>
        !string.IsNullOrWhiteSpace(data) &&
        (data.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
         data.StartsWith("gs://", StringComparison.OrdinalIgnoreCase));

    private static bool IsModelFetchedAttachmentReference(string? data) =>
        !string.IsNullOrWhiteSpace(data) &&
        (data.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         data.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         data.StartsWith("gs://", StringComparison.OrdinalIgnoreCase));

    private static bool IsExternalHttpsUrl(string data) =>
        data.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
        !data.StartsWith("https://generativelanguage.googleapis.com/", StringComparison.OrdinalIgnoreCase);

    private sealed class AttachmentMetadata
    {
        [JsonPropertyName("attachments")]
        public List<PersistedAttachment>? Attachments { get; set; }
    }

    private sealed class PersistedAttachment
    {
        [JsonPropertyName("mime")]
        public string? Mime { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}
