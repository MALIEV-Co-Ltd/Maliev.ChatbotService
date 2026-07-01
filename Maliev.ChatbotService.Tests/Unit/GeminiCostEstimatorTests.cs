using Maliev.ChatbotService.Application.Costing;
using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiCostEstimatorTests
{
    [Fact]
    public void Estimate_FlashFlexUsageWithCachedAudioAndText_UsesTierAndModalityRates()
    {
        var usage = new GeminiTokenUsage
        {
            PromptTokens = 1200,
            CachedPromptTokens = 300,
            CompletionTokens = 200,
            ThoughtTokens = 40,
            TotalTokens = 1440,
            PromptTokenDetails =
            [
                new GeminiModalityTokenCount { Modality = "TEXT", TokenCount = 800 },
                new GeminiModalityTokenCount { Modality = "AUDIO", TokenCount = 400 }
            ],
            CachedTokenDetails =
            [
                new GeminiModalityTokenCount { Modality = "TEXT", TokenCount = 200 },
                new GeminiModalityTokenCount { Modality = "AUDIO", TokenCount = 100 }
            ]
        };

        var estimate = GeminiCostEstimator.Estimate("gemini-2.5-flash", "flex", usage);

        Assert.NotNull(estimate);
        Assert.Equal("gemini-2.5-flash", estimate!.ModelName);
        Assert.Equal("flex", estimate.ServiceTier);
        Assert.Equal(900, estimate.UncachedPromptTokens);
        Assert.Equal(300, estimate.CachedPromptTokens);
        Assert.Equal(240, estimate.OutputTokens);
        Assert.Equal(240, estimate.UncachedPromptMicroUsd);
        Assert.Equal(16, estimate.CachedPromptMicroUsd);
        Assert.Equal(300, estimate.OutputMicroUsd);
        Assert.Equal(556, estimate.TotalMicroUsd);
    }

    [Fact]
    public void Estimate_UnknownModel_ReturnsNull()
    {
        var estimate = GeminiCostEstimator.Estimate(
            "custom-model",
            "standard",
            new GeminiTokenUsage { PromptTokens = 100, TotalTokens = 100 });

        Assert.Null(estimate);
    }

    [Fact]
    public void Estimate_TotalOnlyUsage_UsesConservativeOutputRate()
    {
        var estimate = GeminiCostEstimator.Estimate(
            "gemini-2.5-flash",
            null,
            new GeminiTokenUsage { TotalTokens = 100 });

        Assert.NotNull(estimate);
        Assert.Equal("standard", estimate!.ServiceTier);
        Assert.Equal(0, estimate.UncachedPromptTokens);
        Assert.Equal(0, estimate.CachedPromptTokens);
        Assert.Equal(100, estimate.OutputTokens);
        Assert.Equal(250, estimate.OutputMicroUsd);
        Assert.Equal(250, estimate.TotalMicroUsd);
    }
}
