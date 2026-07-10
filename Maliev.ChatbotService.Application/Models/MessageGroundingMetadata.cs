using System.Text.Json;

namespace Maliev.ChatbotService.Application.Models;

/// <summary>
/// Reads bounded customer-safe grounding provenance from persisted assistant-message metadata.
/// </summary>
public static class MessageGroundingMetadata
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reads persisted grounding provenance, returning <see langword="null"/> for missing or malformed metadata.
    /// </summary>
    /// <param name="metadataJson">The persisted assistant-message metadata JSON.</param>
    /// <returns>Bounded provenance suitable for customer history and continuation turns.</returns>
    public static GroundingProvenance? TryReadProvenance(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson) || metadataJson.Length > 1_000_000)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!TryGetProperty(document.RootElement, "groundingMetadata", out var groundingMetadata) ||
                groundingMetadata.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(groundingMetadata, "provenance", out var provenanceElement) ||
                provenanceElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var provenance = provenanceElement.Deserialize<GroundingProvenance>(JsonOptions);
            if (provenance is null ||
                string.IsNullOrWhiteSpace(provenance.Purpose) ||
                string.IsNullOrWhiteSpace(provenance.Status))
            {
                return null;
            }

            return new GroundingProvenance
            {
                Purpose = provenance.Purpose[..Math.Min(provenance.Purpose.Length, 80)],
                Provider = string.IsNullOrWhiteSpace(provenance.Provider)
                    ? "google_search"
                    : provenance.Provider[..Math.Min(provenance.Provider.Length, 80)],
                Status = provenance.Status[..Math.Min(provenance.Status.Length, 40)],
                AddressDigest = ShippingGroundingIdentity.IsValidDigest(provenance.AddressDigest)
                    ? provenance.AddressDigest!.ToLowerInvariant()
                    : null,
                ErrorCode = string.IsNullOrWhiteSpace(provenance.ErrorCode)
                    ? null
                    : provenance.ErrorCode[..Math.Min(provenance.ErrorCode.Length, 120)],
                Queries = (provenance.Queries ?? [])
                    .Where(query => !string.IsNullOrWhiteSpace(query))
                    .Select(query => query.Trim()[..Math.Min(query.Trim().Length, 500)])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList(),
                Sources = (provenance.Sources ?? [])
                    .Where(source =>
                        Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) &&
                        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
