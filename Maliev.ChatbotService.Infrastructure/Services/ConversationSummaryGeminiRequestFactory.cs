using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.Services;

internal static class ConversationSummaryGeminiRequestFactory
{
    internal const int MaxSummaryPromptTokens = 30000;
    internal const int MaxSummaryMessages = 1000;
    internal const string EmptySummaryJson = "{\"topics\":[],\"decisions\":[],\"preferences\":[],\"entities\":[],\"intentCategories\":[],\"unresolvedQuestions\":[]}";

    private static readonly object SummaryResponseSchema = new
    {
        type = "object",
        properties = new
        {
            topics = new { type = "array", items = new { type = "string" } },
            decisions = new { type = "array", items = new { type = "string" } },
            preferences = new { type = "array", items = new { type = "string" } },
            entities = new { type = "array", items = new { type = "string" } },
            intentCategories = new { type = "array", items = new { type = "string" } },
            unresolvedQuestions = new { type = "array", items = new { type = "string" } }
        },
        required = new[]
        {
            "topics",
            "decisions",
            "preferences",
            "entities",
            "intentCategories",
            "unresolvedQuestions"
        }
    };

    internal static GeminiRequest CreateRequest(string conversationText, string modelName)
    {
        var systemInstruction = @"You are a conversation summarization assistant. Your task is to analyze a conversation and extract structured information in JSON format.

Extract the following:
1. topics: Array of main topics discussed (e.g., [""CNC machining"", ""material selection""])
2. decisions: Array of decisions made (e.g., [""Customer prefers aluminum"", ""Needs quotation for 100 units""])
3. preferences: Array of user preferences (e.g., [""Prefers 6061 aluminum"", ""Requires anodized finish""])
4. entities: Array of important entities mentioned (e.g., [""100 units"", ""6061 aluminum"", ""delivery by March 15""])
5. intentCategories: Array of intent categories (e.g., [""quotation_request"", ""technical_inquiry"", ""order_status""])
6. unresolvedQuestions: Array of questions that were not answered (e.g., [""What is the lead time?"", ""Can you provide samples?""])

Respond with ONLY valid JSON. No markdown, no code blocks, just the JSON object.";

        return new GeminiRequest
        {
            ModelName = modelName,
            SystemInstruction = systemInstruction,
            Messages = new List<GeminiMessage>
            {
                new()
                {
                    Role = "user",
                    Content = $"Summarize this conversation:\n\n{conversationText}"
                }
            },
            ResponseMimeType = "application/json",
            ResponseSchema = SummaryResponseSchema,
            Temperature = 0.1,
            ThinkingBudget = 0,
            MaxTokens = 1024,
            MaxPromptTokens = MaxSummaryPromptTokens,
            ServiceTier = "flex",
            TimeoutSeconds = GeminiRequest.FlexInferenceTimeoutSeconds
        };
    }

    internal static string BuildConversationText(IEnumerable<Domain.Entities.Message> messages)
    {
        return string.Join("\n", messages.Select(m =>
            $"{(m.Role == Domain.Enums.MessageRole.User ? "User" : "Assistant")}: {m.Content}"));
    }

    internal static string CleanJsonResponse(string content)
    {
        var cleaned = content.Trim();
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned.Substring(7);
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Substring(3);
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        }

        return cleaned.Trim();
    }

    internal static bool IsValidSummaryJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return root.TryGetProperty("topics", out _) &&
                   root.TryGetProperty("decisions", out _) &&
                   root.TryGetProperty("preferences", out _) &&
                   root.TryGetProperty("entities", out _) &&
                   root.TryGetProperty("intentCategories", out _) &&
                   root.TryGetProperty("unresolvedQuestions", out _);
        }
        catch
        {
            return false;
        }
    }
}
