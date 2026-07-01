using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Maliev.ChatbotService.Api.Security;

internal static class StandardWebhookVerifier
{
    private const string SigningSecretPrefix = "whsec_";
    private const string SignatureVersion = "v1";
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    public static bool TryVerify(
        string signingSecret,
        string webhookId,
        string webhookTimestamp,
        string webhookSignature,
        string payload,
        DateTimeOffset now,
        out string failureReason)
    {
        if (string.IsNullOrWhiteSpace(webhookId))
        {
            failureReason = "Missing webhook-id header.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(webhookTimestamp))
        {
            failureReason = "Missing webhook-timestamp header.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(webhookSignature))
        {
            failureReason = "Missing webhook-signature header.";
            return false;
        }

        if (!TryParseTimestamp(webhookTimestamp, now, out failureReason))
        {
            return false;
        }

        if (!TryDecodeSigningSecret(signingSecret, out var secretBytes, out failureReason))
        {
            return false;
        }

        var signedContent = $"{webhookId}.{webhookTimestamp}.{payload}";
        var expectedSignature = ComputeSignature(secretBytes, signedContent);

        foreach (var suppliedSignature in webhookSignature.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryDecodeSignature(suppliedSignature, out var signatureBytes))
            {
                continue;
            }

            if (signatureBytes.Length == expectedSignature.Length &&
                CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            {
                failureReason = string.Empty;
                return true;
            }
        }

        failureReason = "No matching v1 webhook signature.";
        return false;
    }

    private static bool TryParseTimestamp(string webhookTimestamp, DateTimeOffset now, out string failureReason)
    {
        if (!long.TryParse(webhookTimestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            failureReason = "Invalid webhook-timestamp header.";
            return false;
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            failureReason = "Invalid webhook-timestamp header.";
            return false;
        }

        var skew = now - timestamp;
        if (skew.Duration() > TimestampTolerance)
        {
            failureReason = "Webhook timestamp is outside the allowed tolerance.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TryDecodeSigningSecret(
        string signingSecret,
        out byte[] secretBytes,
        out string failureReason)
    {
        secretBytes = [];

        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            failureReason = "Missing webhook signing secret.";
            return false;
        }

        if (!signingSecret.StartsWith(SigningSecretPrefix, StringComparison.Ordinal))
        {
            failureReason = "Webhook signing secret must use the whsec_ prefix.";
            return false;
        }

        try
        {
            secretBytes = Convert.FromBase64String(signingSecret[SigningSecretPrefix.Length..]);
            failureReason = string.Empty;
            return true;
        }
        catch (FormatException)
        {
            failureReason = "Webhook signing secret is not valid base64.";
            return false;
        }
    }

    private static byte[] ComputeSignature(byte[] secretBytes, string signedContent)
    {
        using var hmac = new HMACSHA256(secretBytes);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
    }

    private static bool TryDecodeSignature(string suppliedSignature, out byte[] signatureBytes)
    {
        signatureBytes = [];
        var separatorIndex = suppliedSignature.IndexOf(',', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        if (!suppliedSignature[..separatorIndex].Equals(SignatureVersion, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            signatureBytes = Convert.FromBase64String(suppliedSignature[(separatorIndex + 1)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
