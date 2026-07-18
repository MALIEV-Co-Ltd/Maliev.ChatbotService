using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.ChatbotService.Application.Configuration;

/// <summary>
/// Resolves default Gemini safety settings for native Gemini requests.
/// </summary>
public sealed class GeminiSafetySettingsOptions
{
    private const string EnabledConfigurationKey = "Gemini:SafetySettings:Enabled";
    private const string ThresholdConfigurationKey = "Gemini:SafetySettings:Threshold";
    private const string CategoriesConfigurationKey = "Gemini:SafetySettings:Categories";
    private const string DefaultThreshold = "BLOCK_ONLY_HIGH";

    private static readonly string[] DefaultCategories =
    [
        "HARM_CATEGORY_HARASSMENT",
        "HARM_CATEGORY_HATE_SPEECH",
        "HARM_CATEGORY_SEXUALLY_EXPLICIT",
        "HARM_CATEGORY_DANGEROUS_CONTENT"
    ];

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "HARM_CATEGORY_HARASSMENT",
        "HARM_CATEGORY_HATE_SPEECH",
        "HARM_CATEGORY_SEXUALLY_EXPLICIT",
        "HARM_CATEGORY_DANGEROUS_CONTENT",
        "HARM_CATEGORY_CIVIC_INTEGRITY"
    };

    private static readonly HashSet<string> AllowedThresholds = new(StringComparer.OrdinalIgnoreCase)
    {
        "HARM_BLOCK_THRESHOLD_UNSPECIFIED",
        "BLOCK_LOW_AND_ABOVE",
        "BLOCK_MEDIUM_AND_ABOVE",
        "BLOCK_ONLY_HIGH",
        "BLOCK_NONE",
        "OFF"
    };

    private GeminiSafetySettingsOptions(IReadOnlyList<GeminiSafetySetting> safetySettings)
    {
        SafetySettings = safetySettings;
    }

    /// <summary>
    /// Gets the configured default safety settings.
    /// </summary>
    public IReadOnlyList<GeminiSafetySetting> SafetySettings { get; }

    /// <summary>
    /// Creates Gemini safety settings from configuration.
    /// </summary>
    public static GeminiSafetySettingsOptions FromConfiguration(IConfiguration? configuration)
    {
        if (configuration?.GetValue<bool?>(EnabledConfigurationKey) != true)
        {
            return new GeminiSafetySettingsOptions([]);
        }

        var threshold = NormalizeThreshold(configuration[ThresholdConfigurationKey] ?? DefaultThreshold);
        var configuredCategories = configuration
            .GetSection(CategoriesConfigurationKey)
            .Get<string[]>();
        var categories = configuredCategories is { Length: > 0 }
            ? configuredCategories
            : DefaultCategories;

        var settings = categories
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(NormalizeCategory)
            .Distinct(StringComparer.Ordinal)
            .Select(category => new GeminiSafetySetting
            {
                Category = category,
                Threshold = threshold
            })
            .ToList();

        return new GeminiSafetySettingsOptions(settings);
    }

    private static string NormalizeCategory(string category)
    {
        var normalized = category.Trim().ToUpperInvariant();
        if (!AllowedCategories.Contains(normalized))
        {
            throw new InvalidOperationException(
                $"{CategoriesConfigurationKey} contains unsupported Gemini harm category '{category}'.");
        }

        return normalized;
    }

    private static string NormalizeThreshold(string threshold)
    {
        var normalized = threshold.Trim().ToUpperInvariant();
        if (!AllowedThresholds.Contains(normalized))
        {
            throw new InvalidOperationException(
                $"{ThresholdConfigurationKey} must be one of: {string.Join(", ", AllowedThresholds)}.");
        }

        return normalized;
    }
}
