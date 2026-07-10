using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Application.Models;

internal static class GeminiStreamUsage
{
    internal static void Merge(GeminiResponse target, GeminiResponse source)
    {
        target.TokenUsage = source.TokenUsage ?? target.TokenUsage;
        target.ServiceTier = source.ServiceTier ?? target.ServiceTier;
        target.GoogleSearchGroundingPromptCount = Math.Max(
            target.GoogleSearchGroundingPromptCount,
            source.GoogleSearchGroundingPromptCount);
        AddDistinct(target.GroundingWebSearchQueries, source.GroundingWebSearchQueries);
        foreach (var sourceItem in source.GroundingSources)
        {
            if (!target.GroundingSources.Any(existing =>
                    string.Equals(existing.Url, sourceItem.Url, StringComparison.OrdinalIgnoreCase)))
            {
                target.GroundingSources.Add(sourceItem);
            }
        }
    }

    internal static bool HasKnownUsage(GeminiResponse response) =>
        response.TokenUsage is not null ||
        response.GoogleSearchGroundingPromptCount > 0 ||
        response.GroundingWebSearchQueries.Count > 0 ||
        response.GroundingSources.Count > 0;

    private static void AddDistinct(List<string> target, IEnumerable<string> source)
    {
        foreach (var item in source)
        {
            if (!string.IsNullOrWhiteSpace(item) &&
                !target.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(item);
            }
        }
    }
}

internal sealed class GeminiUsageCanceledException : OperationCanceledException
{
    internal GeminiUsageCanceledException(
        OperationCanceledException innerException,
        GeminiResponse partialResponse,
        CancellationToken cancellationToken)
        : base("Gemini streaming was canceled after billable usage was reported.", innerException, cancellationToken)
    {
        PartialResponse = partialResponse;
    }

    internal GeminiResponse PartialResponse { get; }
}
