using Microsoft.Extensions.Configuration;

namespace Maliev.ChatbotService.Infrastructure.AI;

internal static class GeminiApiConfiguration
{
    internal static string ResolveApiKey(IConfiguration configuration)
    {
        var apiKey = FirstNonEmpty(
            configuration["Gemini:ApiKey"],
            configuration["Llm:OpenAICompatible:ApiKey"],
            configuration["OpenAICompatible:ApiKey"]);

        return apiKey ?? throw new InvalidOperationException(
            "Gemini API key is not configured. Set 'Gemini:ApiKey' or 'Llm:OpenAICompatible:ApiKey'.");
    }

    internal static string ResolveMainModelName(IConfiguration configuration) =>
        FirstNonEmpty(
            configuration["Gemini:MainModelName"],
            configuration["Llm:OpenAICompatible:ModelName"],
            configuration["OpenAICompatible:ModelName"])
        ?? "gemini-2.5-flash";

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
