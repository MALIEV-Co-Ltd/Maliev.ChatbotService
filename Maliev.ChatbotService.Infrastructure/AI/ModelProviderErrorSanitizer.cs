using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.AI;

internal static class ModelProviderErrorSanitizer
{
    public static string Summarize(HttpResponseMessage response, string? responseContent)
    {
        var bodyBytes = string.IsNullOrEmpty(responseContent)
            ? 0
            : Encoding.UTF8.GetByteCount(responseContent);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
        var providerCode = TryExtractProviderCode(responseContent);
        var providerStatus = TryExtractProviderStatus(responseContent);
        var providerDetails = string.Join(
            ", ",
            new[]
            {
                string.IsNullOrWhiteSpace(providerCode) ? null : $"providerCode={providerCode}",
                string.IsNullOrWhiteSpace(providerStatus) ? null : $"providerStatus={providerStatus}"
            }.Where(item => item is not null));

        return string.IsNullOrWhiteSpace(providerDetails)
            ? $"status={(int)response.StatusCode} {response.StatusCode}; contentType={contentType}; bodyBytes={bodyBytes}"
            : $"status={(int)response.StatusCode} {response.StatusCode}; contentType={contentType}; bodyBytes={bodyBytes}; {providerDetails}";
    }

    private static string? TryExtractProviderCode(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out var nestedCode))
            {
                return GetScalarValue(nestedCode);
            }

            return root.TryGetProperty("code", out var topLevelCode)
                ? GetScalarValue(topLevelCode)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryExtractProviderStatus(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("status", out var nestedStatus))
            {
                return GetScalarValue(nestedStatus);
            }

            return root.TryGetProperty("status", out var topLevelStatus)
                ? GetScalarValue(topLevelStatus)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetScalarValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }
}
