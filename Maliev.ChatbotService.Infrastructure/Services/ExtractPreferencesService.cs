using System.Text.Json;
using System.Text.RegularExpressions;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for extracting user preferences from conversation messages using pattern matching.
/// </summary>
public partial class ExtractPreferencesService : IExtractPreferencesService
{
    private readonly ILogger<ExtractPreferencesService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractPreferencesService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ExtractPreferencesService(ILogger<ExtractPreferencesService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts preferences from a user message.
    /// </summary>
    /// <param name="message">The user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of extracted preferences with confidence scores.</returns>
    public Task<List<ExtractedPreference>> ExtractPreferencesAsync(string message, CancellationToken cancellationToken = default)
    {
        var preferences = new List<ExtractedPreference>();

        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.FromResult(preferences);
        }

        var messageLower = message.ToLowerInvariant();

        preferences.AddRange(ExtractMaterialPreferences(message, messageLower));
        preferences.AddRange(ExtractProcessPreferences(message, messageLower));
        preferences.AddRange(ExtractQualityPreferences(message, messageLower));
        preferences.AddRange(ExtractDeliveryPreferences(message, messageLower));
        preferences.AddRange(ExtractGeneralPreferences(message, messageLower));

        _logger.LogInformation("Extracted {Count} preferences from message", preferences.Count);

        return Task.FromResult(preferences);
    }

    private List<ExtractedPreference> ExtractMaterialPreferences(string message, string messageLower)
    {
        var preferences = new List<ExtractedPreference>();

        var materialPatterns = new Dictionary<Regex, double>
        {
            { MaterialPreferenceRegex(), 0.95 },
            { MaterialLikeRegex(), 0.90 },
            { MaterialAlwaysRegex(), 0.95 },
            { MaterialUsuallyRegex(), 0.85 }
        };

        foreach (var (pattern, baseConfidence) in materialPatterns)
        {
            var match = pattern.Match(messageLower);
            if (match.Success)
            {
                var material = match.Groups[1].Value.Trim();
                preferences.Add(new ExtractedPreference
                {
                    Key = "MaterialPreference",
                    Value = JsonSerializer.Serialize(new { material }),
                    Confidence = baseConfidence
                });
            }
        }

        return preferences;
    }

    private List<ExtractedPreference> ExtractProcessPreferences(string message, string messageLower)
    {
        var preferences = new List<ExtractedPreference>();

        var processPattern = ProcessPreferenceRegex();
        var match = processPattern.Match(messageLower);
        if (match.Success)
        {
            var process = match.Groups[1].Value.Trim();
            preferences.Add(new ExtractedPreference
            {
                Key = "ProcessPreference",
                Value = JsonSerializer.Serialize(new { process }),
                Confidence = 0.90
            });
        }

        return preferences;
    }

    private List<ExtractedPreference> ExtractQualityPreferences(string message, string messageLower)
    {
        var preferences = new List<ExtractedPreference>();

        var qualityPattern = QualityStandardRegex();
        var match = qualityPattern.Match(messageLower);
        if (match.Success)
        {
            var standard = match.Groups[1].Value.Trim().ToUpperInvariant();
            preferences.Add(new ExtractedPreference
            {
                Key = "QualityStandard",
                Value = JsonSerializer.Serialize(new { standard }),
                Confidence = 0.92
            });
        }

        return preferences;
    }

    private List<ExtractedPreference> ExtractDeliveryPreferences(string message, string messageLower)
    {
        var preferences = new List<ExtractedPreference>();

        var deliveryPattern = DeliveryPreferenceRegex();
        var match = deliveryPattern.Match(messageLower);
        if (match.Success)
        {
            var timeframe = match.Groups[1].Value.Trim();
            preferences.Add(new ExtractedPreference
            {
                Key = "DeliveryPreference",
                Value = JsonSerializer.Serialize(new { timeframe }),
                Confidence = 0.88
            });
        }

        return preferences;
    }

    private List<ExtractedPreference> ExtractGeneralPreferences(string message, string messageLower)
    {
        var preferences = new List<ExtractedPreference>();

        if (messageLower.Contains("prefer") || messageLower.Contains("like to use") || messageLower.Contains("always use"))
        {
            var quantityPattern = QuantityPreferenceRegex();
            var match = quantityPattern.Match(messageLower);
            if (match.Success)
            {
                var quantity = match.Groups[1].Value.Trim();
                preferences.Add(new ExtractedPreference
                {
                    Key = "TypicalQuantity",
                    Value = JsonSerializer.Serialize(new { quantity }),
                    Confidence = 0.85
                });
            }
        }

        return preferences;
    }

    [GeneratedRegex(@"(?:i\s+prefer|my\s+preference\s+is|i\s+want)\s+(?:to\s+use\s+)?([a-z0-9\s-]+?)\s+(?:material|metal|steel|aluminum|plastic)")]
    private static partial Regex MaterialPreferenceRegex();

    [GeneratedRegex(@"(?:i\s+like|i\s+love)\s+(?:using\s+)?([a-z0-9\s-]+?)\s+(?:material|metal|steel|aluminum|plastic)")]
    private static partial Regex MaterialLikeRegex();

    [GeneratedRegex(@"(?:i\s+always\s+use|always\s+go\s+with)\s+([a-z0-9\s-]+?)\s+(?:for|material)")]
    private static partial Regex MaterialAlwaysRegex();

    [GeneratedRegex(@"(?:i\s+usually\s+use|typically\s+use)\s+([a-z0-9\s-]+?)\s+(?:for|material)")]
    private static partial Regex MaterialUsuallyRegex();

    [GeneratedRegex(@"(?:i\s+prefer|my\s+preference\s+is)\s+([a-z\s-]+?)\s+(?:process|machining|manufacturing)")]
    private static partial Regex ProcessPreferenceRegex();

    [GeneratedRegex(@"(?:require|need|must\s+meet)\s+(iso|astm|din)\s*[\d-]*")]
    private static partial Regex QualityStandardRegex();

    [GeneratedRegex(@"(?:i\s+need\s+it|delivery)\s+(?:in|within)\s+([0-9]+\s+(?:days?|weeks?|months?))")]
    private static partial Regex DeliveryPreferenceRegex();

    [GeneratedRegex(@"(?:typically|usually|normally)\s+(?:order|need)\s+(?:about|around)?\s*([0-9,]+)\s+(?:pieces|units|parts)")]
    private static partial Regex QuantityPreferenceRegex();
}
