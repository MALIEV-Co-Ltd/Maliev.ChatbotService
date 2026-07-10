using System.Security.Cryptography;
using System.Text;

namespace Maliev.ChatbotService.Application.Models;

internal static class ShippingGroundingIdentity
{
    internal static string? CreateDigest(string? addressInput)
    {
        if (string.IsNullOrWhiteSpace(addressInput))
        {
            return null;
        }

        var normalized = addressInput.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var canonical = new string(normalized
            .Where(character => !char.IsWhiteSpace(character) && !char.IsControl(character))
            .ToArray());
        if (canonical.Length == 0)
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    internal static bool IsValidDigest(string? digest) =>
        digest is { Length: 64 } && digest.All(Uri.IsHexDigit);

    internal static bool Matches(string? expected, string? actual) =>
        IsValidDigest(expected) &&
        IsValidDigest(actual) &&
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
