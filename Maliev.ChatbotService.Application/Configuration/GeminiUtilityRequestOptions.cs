using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.ChatbotService.Application.Configuration;

/// <summary>
/// Resolves Gemini request settings for bounded utility calls.
/// </summary>
public sealed class GeminiUtilityRequestOptions
{
    private const int DefaultTimeoutSeconds = 5;
    private const string ServiceTierConfigurationKey = "Gemini:UtilityRequests:ServiceTier";

    private GeminiUtilityRequestOptions(string? serviceTier, int timeoutSeconds)
    {
        ServiceTier = serviceTier;
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Gets the Gemini service tier to request, or null for provider default.
    /// </summary>
    public string? ServiceTier { get; }

    /// <summary>
    /// Gets the per-request timeout for the utility call.
    /// </summary>
    public int TimeoutSeconds { get; }

    /// <summary>
    /// Creates utility request options from configuration.
    /// </summary>
    public static GeminiUtilityRequestOptions FromConfiguration(IConfiguration? configuration)
    {
        var serviceTier = NormalizeServiceTier(configuration?[ServiceTierConfigurationKey]);
        var timeoutSeconds = string.Equals(serviceTier, "flex", StringComparison.OrdinalIgnoreCase)
            ? GeminiRequest.FlexInferenceTimeoutSeconds
            : DefaultTimeoutSeconds;

        return new GeminiUtilityRequestOptions(serviceTier, timeoutSeconds);
    }

    private static string? NormalizeServiceTier(string? serviceTier)
    {
        if (string.IsNullOrWhiteSpace(serviceTier))
        {
            return null;
        }

        return serviceTier.Trim();
    }
}
