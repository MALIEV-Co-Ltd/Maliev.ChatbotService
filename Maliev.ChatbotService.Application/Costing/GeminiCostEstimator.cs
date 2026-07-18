using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Application.Costing;

/// <summary>
/// Estimates Gemini Developer API paid-tier token cost from reported usage metadata.
/// </summary>
public static class GeminiCostEstimator
{
    /// <summary>
    /// Gets the pricing table basis used by this estimator.
    /// </summary>
    public const string PricingBasis = "Gemini Developer API paid-tier token and Google Search grounding pricing verified 2026-07-01";

    private const string StandardTier = "standard";
    private const string FlexTier = "flex";
    private const string BatchTier = "batch";
    private const string PriorityTier = "priority";

    private static readonly Dictionary<string, ModelPricing> PricingByModel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gemini-2.5-flash"] = new ModelPricing(
            new Dictionary<string, TokenRates>(StringComparer.OrdinalIgnoreCase)
            {
                [StandardTier] = new TokenRates(0.30m, 1.00m, 0.03m, 0.10m, 2.50m, 35m),
                [FlexTier] = new TokenRates(0.15m, 0.50m, 0.03m, 0.10m, 1.25m, 35m),
                [BatchTier] = new TokenRates(0.15m, 0.50m, 0.03m, 0.10m, 1.25m, 35m),
                [PriorityTier] = new TokenRates(0.54m, 1.80m, 0.054m, 0.18m, 4.50m, 35m)
            },
            1.00m),
        ["gemini-2.5-flash-lite"] = new ModelPricing(
            new Dictionary<string, TokenRates>(StringComparer.OrdinalIgnoreCase)
            {
                [StandardTier] = new TokenRates(0.10m, 0.30m, 0.01m, 0.03m, 0.40m, 35m),
                [FlexTier] = new TokenRates(0.05m, 0.15m, 0.01m, 0.03m, 0.20m, 35m),
                [BatchTier] = new TokenRates(0.05m, 0.15m, 0.01m, 0.03m, 0.20m, 35m),
                [PriorityTier] = new TokenRates(0.18m, 0.54m, 0.018m, 0.054m, 0.72m, 35m)
            },
            1.00m)
    };

    /// <summary>
    /// Estimates token cost in micro-USD for a Gemini response.
    /// </summary>
    /// <param name="modelName">The Gemini model name used for the request.</param>
    /// <param name="serviceTier">The Gemini service tier. Null and unspecified resolve to standard.</param>
    /// <param name="usage">The token usage metadata reported by Gemini.</param>
    /// <param name="googleSearchGroundingPromptCount">The count of grounded prompts with Google Search metadata.</param>
    /// <returns>The cost estimate, or null when the model or tier is unknown.</returns>
    public static GeminiCostEstimate? Estimate(
        string? modelName,
        string? serviceTier,
        GeminiTokenUsage? usage,
        int googleSearchGroundingPromptCount = 0)
    {
        if (usage is null && googleSearchGroundingPromptCount <= 0)
        {
            return null;
        }

        var normalizedModelName = NormalizeModelName(modelName);
        if (string.IsNullOrWhiteSpace(normalizedModelName) ||
            !PricingByModel.TryGetValue(normalizedModelName, out var modelPricing))
        {
            return null;
        }

        var normalizedTier = NormalizeServiceTier(serviceTier);
        if (!modelPricing.Tiers.TryGetValue(normalizedTier, out var rates))
        {
            return null;
        }

        usage ??= new GeminiTokenUsage();
        var boundedGroundingPromptCount = Math.Max(0, googleSearchGroundingPromptCount);
        var fallbackOutputTokens = ShouldUseTotalOnlyFallback(usage)
            ? Math.Max(0, usage.TotalTokens)
            : 0;
        var promptTokens = fallbackOutputTokens > 0 ? 0 : usage.PromptTokens;
        var cachedPromptTokens = fallbackOutputTokens > 0 ? 0 : usage.CachedPromptTokens;
        var toolUsePromptTokens = fallbackOutputTokens > 0 ? 0 : usage.ToolUsePromptTokens;
        var cachedByModality = NormalizeModalityCounts(usage.CachedTokenDetails, cachedPromptTokens);
        var promptByModality = NormalizeModalityCounts(usage.PromptTokenDetails, promptTokens);
        var toolUseByModality = NormalizeModalityCounts(usage.ToolUsePromptTokenDetails, toolUsePromptTokens);
        var uncachedByModality = SubtractCachedTokens(promptByModality, cachedByModality);
        NormalizeTotal(uncachedByModality, Math.Max(0, promptTokens - cachedPromptTokens));

        var outputTokens = fallbackOutputTokens > 0
            ? fallbackOutputTokens
            : Math.Max(0, usage.CompletionTokens) + Math.Max(0, usage.ThoughtTokens);
        var uncachedPromptMicroUsd = EstimateInputMicroUsd(
            uncachedByModality,
            rates.InputTextImageVideoUsdPerMillion,
            rates.InputAudioUsdPerMillion);
        var cachedPromptMicroUsd = EstimateInputMicroUsd(
            cachedByModality,
            rates.CachedTextImageVideoUsdPerMillion,
            rates.CachedAudioUsdPerMillion);
        var toolUsePromptMicroUsd = EstimateInputMicroUsd(
            toolUseByModality,
            rates.InputTextImageVideoUsdPerMillion,
            rates.InputAudioUsdPerMillion);
        var outputMicroUsd = ToMicroUsd(outputTokens, rates.OutputUsdPerMillion);
        var googleSearchGroundingMicroUsd = ToPerThousandMicroUsd(
            boundedGroundingPromptCount,
            rates.GoogleSearchGroundingUsdPerThousandPrompts);

        return new GeminiCostEstimate
        {
            ModelName = normalizedModelName,
            ServiceTier = normalizedTier,
            PricingBasis = PricingBasis,
            UncachedPromptTokens = Math.Max(0, promptTokens - cachedPromptTokens),
            CachedPromptTokens = Math.Max(0, cachedPromptTokens),
            ToolUsePromptTokens = Math.Max(0, toolUsePromptTokens),
            GoogleSearchGroundingPromptCount = boundedGroundingPromptCount,
            OutputTokens = outputTokens,
            UncachedPromptMicroUsd = uncachedPromptMicroUsd,
            CachedPromptMicroUsd = cachedPromptMicroUsd,
            ToolUsePromptMicroUsd = toolUsePromptMicroUsd,
            GoogleSearchGroundingMicroUsd = googleSearchGroundingMicroUsd,
            OutputMicroUsd = outputMicroUsd,
            TotalMicroUsd = uncachedPromptMicroUsd +
                cachedPromptMicroUsd +
                toolUsePromptMicroUsd +
                googleSearchGroundingMicroUsd +
                outputMicroUsd
        };
    }

    /// <summary>
    /// Estimates explicit Gemini cached-content storage cost for a cache lifetime.
    /// </summary>
    /// <param name="modelName">The Gemini model name used for the cached content.</param>
    /// <param name="cachedInputTokens">The number of input tokens stored in the cache.</param>
    /// <param name="ttl">The provider cache time to live.</param>
    /// <returns>The storage estimate, or null when the model or inputs are not estimable.</returns>
    public static GeminiContextCacheStorageEstimate? EstimateContextCacheStorage(
        string? modelName,
        int cachedInputTokens,
        TimeSpan ttl)
    {
        var normalizedModelName = NormalizeModelName(modelName);
        if (string.IsNullOrWhiteSpace(normalizedModelName) ||
            cachedInputTokens <= 0 ||
            ttl <= TimeSpan.Zero ||
            !PricingByModel.TryGetValue(normalizedModelName, out var modelPricing))
        {
            return null;
        }

        var ttlHours = (decimal)ttl.TotalHours;
        var storageMicroUsd = ToMicroUsd(cachedInputTokens, modelPricing.ContextCacheStorageUsdPerMillionTokenHours, ttlHours);

        return new GeminiContextCacheStorageEstimate
        {
            ModelName = normalizedModelName,
            PricingBasis = PricingBasis,
            CachedInputTokens = cachedInputTokens,
            TtlSeconds = (int)Math.Ceiling(ttl.TotalSeconds),
            StorageMicroUsd = storageMicroUsd
        };
    }

    private static bool ShouldUseTotalOnlyFallback(GeminiTokenUsage usage)
    {
        return usage.TotalTokens > 0 &&
            usage.PromptTokens <= 0 &&
            usage.CachedPromptTokens <= 0 &&
            usage.ToolUsePromptTokens <= 0 &&
            usage.CompletionTokens <= 0 &&
            usage.ThoughtTokens <= 0 &&
            usage.PromptTokenDetails.Count == 0 &&
            usage.CachedTokenDetails.Count == 0 &&
            usage.ToolUsePromptTokenDetails.Count == 0 &&
            usage.CandidateTokenDetails.Count == 0;
    }

    private static Dictionary<string, int> NormalizeModalityCounts(
        IReadOnlyCollection<GeminiModalityTokenCount> details,
        int fallbackTokens)
    {
        if (details.Count == 0)
        {
            return fallbackTokens > 0
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["TEXT"] = fallbackTokens }
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var detail in details)
        {
            if (detail.TokenCount <= 0)
            {
                continue;
            }

            var modality = string.IsNullOrWhiteSpace(detail.Modality)
                ? "TEXT"
                : detail.Modality.Trim().ToUpperInvariant();
            result[modality] = result.TryGetValue(modality, out var existing)
                ? existing + detail.TokenCount
                : detail.TokenCount;
        }

        NormalizeTotal(result, fallbackTokens);
        return result;
    }

    private static Dictionary<string, int> SubtractCachedTokens(
        Dictionary<string, int> promptByModality,
        Dictionary<string, int> cachedByModality)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (modality, tokens) in promptByModality)
        {
            cachedByModality.TryGetValue(modality, out var cachedTokens);
            var uncachedTokens = Math.Max(0, tokens - cachedTokens);
            if (uncachedTokens > 0)
            {
                result[modality] = uncachedTokens;
            }
        }

        return result;
    }

    private static void NormalizeTotal(Dictionary<string, int> countsByModality, int totalTokens)
    {
        if (totalTokens <= 0)
        {
            countsByModality.Clear();
            return;
        }

        var currentTotal = countsByModality.Values.Sum();
        if (currentTotal < totalTokens)
        {
            countsByModality["TEXT"] = countsByModality.TryGetValue("TEXT", out var existing)
                ? existing + totalTokens - currentTotal
                : totalTokens - currentTotal;
        }
    }

    private static long EstimateInputMicroUsd(
        IReadOnlyDictionary<string, int> tokensByModality,
        decimal textImageVideoRate,
        decimal audioRate)
    {
        var total = 0L;
        foreach (var (modality, tokens) in tokensByModality)
        {
            var rate = modality.Equals("AUDIO", StringComparison.OrdinalIgnoreCase)
                ? audioRate
                : textImageVideoRate;
            total += ToMicroUsd(tokens, rate);
        }

        return total;
    }

    private static long ToMicroUsd(int tokens, decimal usdPerMillionTokens)
    {
        if (tokens <= 0)
        {
            return 0;
        }

        return (long)Math.Round(tokens * usdPerMillionTokens, MidpointRounding.AwayFromZero);
    }

    private static long ToMicroUsd(int tokens, decimal usdPerMillionTokenHours, decimal hours)
    {
        if (tokens <= 0 || hours <= 0)
        {
            return 0;
        }

        return (long)Math.Round(tokens * usdPerMillionTokenHours * hours, MidpointRounding.AwayFromZero);
    }

    private static long ToPerThousandMicroUsd(int count, decimal usdPerThousand)
    {
        if (count <= 0)
        {
            return 0;
        }

        return (long)Math.Round(count * usdPerThousand * 1000m, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeServiceTier(string? serviceTier)
    {
        if (string.IsNullOrWhiteSpace(serviceTier) ||
            serviceTier.Equals("unspecified", StringComparison.OrdinalIgnoreCase))
        {
            return StandardTier;
        }

        return serviceTier.Trim().ToLowerInvariant();
    }

    private static string? NormalizeModelName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        var normalized = modelName.Trim();
        return normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? normalized["models/".Length..]
            : normalized;
    }

    private sealed record ModelPricing(
        IReadOnlyDictionary<string, TokenRates> Tiers,
        decimal ContextCacheStorageUsdPerMillionTokenHours);

    private sealed record TokenRates(
        decimal InputTextImageVideoUsdPerMillion,
        decimal InputAudioUsdPerMillion,
        decimal CachedTextImageVideoUsdPerMillion,
        decimal CachedAudioUsdPerMillion,
        decimal OutputUsdPerMillion,
        decimal GoogleSearchGroundingUsdPerThousandPrompts);
}

/// <summary>
/// Gemini token cost estimate expressed in micro-USD.
/// </summary>
public sealed class GeminiCostEstimate
{
    /// <summary>Gets or sets the normalized Gemini model name.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized billing tier.</summary>
    public string ServiceTier { get; set; } = string.Empty;

    /// <summary>Gets or sets the pricing table basis for the estimate.</summary>
    public string PricingBasis { get; set; } = string.Empty;

    /// <summary>Gets or sets non-cached prompt tokens charged at input rates.</summary>
    public int UncachedPromptTokens { get; set; }

    /// <summary>Gets or sets cached prompt tokens charged at cache-read rates.</summary>
    public int CachedPromptTokens { get; set; }

    /// <summary>Gets or sets tool-use prompt tokens charged at input rates.</summary>
    public int ToolUsePromptTokens { get; set; }

    /// <summary>Gets or sets Google Search grounded prompts charged at grounding rates.</summary>
    public int GoogleSearchGroundingPromptCount { get; set; }

    /// <summary>Gets or sets generated output plus thinking tokens charged at output rates.</summary>
    public int OutputTokens { get; set; }

    /// <summary>Gets or sets estimated non-cached prompt cost in micro-USD.</summary>
    public long UncachedPromptMicroUsd { get; set; }

    /// <summary>Gets or sets estimated cached prompt cost in micro-USD.</summary>
    public long CachedPromptMicroUsd { get; set; }

    /// <summary>Gets or sets estimated tool-use prompt cost in micro-USD.</summary>
    public long ToolUsePromptMicroUsd { get; set; }

    /// <summary>Gets or sets estimated Google Search grounding cost in micro-USD.</summary>
    public long GoogleSearchGroundingMicroUsd { get; set; }

    /// <summary>Gets or sets estimated output plus thinking cost in micro-USD.</summary>
    public long OutputMicroUsd { get; set; }

    /// <summary>Gets or sets estimated total token cost in micro-USD.</summary>
    public long TotalMicroUsd { get; set; }
}

/// <summary>
/// Gemini explicit context-cache storage cost estimate expressed in micro-USD.
/// </summary>
public sealed class GeminiContextCacheStorageEstimate
{
    /// <summary>Gets or sets the normalized Gemini model name.</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Gets or sets the pricing table basis for the estimate.</summary>
    public string PricingBasis { get; set; } = string.Empty;

    /// <summary>Gets or sets input tokens stored in the provider cache.</summary>
    public int CachedInputTokens { get; set; }

    /// <summary>Gets or sets the provider cache time to live in seconds.</summary>
    public int TtlSeconds { get; set; }

    /// <summary>Gets or sets estimated cache storage cost in micro-USD.</summary>
    public long StorageMicroUsd { get; set; }
}
