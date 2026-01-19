using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Implementation of intent classification using Gemini model.
/// </summary>
public class IntentClassificationService : IIntentClassificationService
{
    private readonly IGeminiClient _geminiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IntentClassificationService> _logger;
    private readonly string _modelName;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntentClassificationService"/> class.
    /// </summary>
    public IntentClassificationService(
        IGeminiClient geminiClient,
        IConfiguration configuration,
        ILogger<IntentClassificationService> logger)
    {
        _geminiClient = geminiClient;
        _configuration = configuration;
        _logger = logger;
        _modelName = _configuration["IntentClassification:ModelName"] ?? "gemini-2.5-flash-lite";
    }

    /// <inheritdoc/>
    public async Task<IntentClassificationResult> ClassifyIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Classifying intent for message: {Message}", message);

        var systemInstruction = @"You are an intent classifier for Maliev Manufacturing Chatbot.
Your task is to identify the specialized topic of the user's message.

Available Topics:
- '3D-Scanning': Questions about 3D scanning services, on-site or in-house.
- 'Pricing': Questions about costs, pricing tiers, or service fees.
- 'CNC-Machining': Questions about computer numerical control machining.
- 'General': Greeting, small talk, or general manufacturing questions.

Response Format:
Return ONLY a JSON object with the following fields:
{
  ""intent"": ""Primary topic key"",
  ""confidence"": 0.95,
  ""additionalTopics"": [""Any other relevant topic keys""]
}

Rules:
1. If no specific topic matches, use 'General'.
2. Be precise with confidence scores.
3. Return ONLY valid JSON.";

        var request = new GeminiRequest
        {
            ModelName = _modelName,
            SystemInstruction = systemInstruction,
            Messages = new List<GeminiMessage>
            {
                new GeminiMessage { Role = "user", Content = message }
            },
            Temperature = 0.1, // Low temperature for deterministic classification
            TimeoutSeconds = 5
        };

        try
        {
            var response = await _geminiClient.SendMessageAsync(request, cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("Gemini intent classification failed: {Error}. Falling back to General.", response.ErrorMessage);
                return new IntentClassificationResult { Intent = "General", Confidence = 0.0 };
            }

            // Extract JSON from response (handling potential markdown blocks)
            var json = CleanJsonResponse(response.Content);
            var result = JsonSerializer.Deserialize<IntentClassificationResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                _logger.LogWarning("Failed to deserialize intent classification result. Raw content: {Content}", response.Content);
                return new IntentClassificationResult { Intent = "General", Confidence = 0.0 };
            }

            _logger.LogInformation("Detected intent: {Intent} (Confidence: {Confidence})", result.Intent, result.Confidence);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during intent classification. Falling back to General.");
            return new IntentClassificationResult { Intent = "General", Confidence = 0.0 };
        }
    }

    private static string CleanJsonResponse(string content)
    {
        var json = content.Trim();
        if (json.StartsWith("```json"))
        {
            json = json.Substring(7);
        }
        if (json.EndsWith("```"))
        {
            json = json.Substring(0, json.Length - 3);
        }
        return json.Trim();
    }
}
