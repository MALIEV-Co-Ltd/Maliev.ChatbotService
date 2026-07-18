namespace Maliev.ChatbotService.Api.Models.Webhooks;

internal sealed class GeminiWebhookEvent
{
    public string? Type { get; set; }

    public string? Version { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public GeminiWebhookData? Data { get; set; }
}

internal sealed class GeminiWebhookData
{
    public string? Id { get; set; }

    public string? OutputFileUri { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}
