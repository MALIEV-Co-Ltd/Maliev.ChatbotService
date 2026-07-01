using Maliev.ChatbotService.Application.Configuration;
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
    private const int MaxClassificationPromptTokens = 4096;

    private static readonly object IntentClassificationSchema = new
    {
        type = "object",
        properties = new
        {
            intent = new { type = "string" },
            confidence = new { type = "number" },
            additionalTopics = new { type = "array", items = new { type = "string" } }
        },
        required = new[] { "intent", "confidence", "additionalTopics" }
    };

    private readonly IGeminiClient _geminiClient;
    private readonly ISystemInstructionRepository _repository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IntentClassificationService> _logger;
    private readonly string _modelName;
    private readonly GeminiUtilityRequestOptions _requestOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntentClassificationService"/> class.
    /// </summary>
    public IntentClassificationService(
        IGeminiClient geminiClient,
        ISystemInstructionRepository repository,
        IConfiguration configuration,
        ILogger<IntentClassificationService> logger)
    {
        _geminiClient = geminiClient;
        _repository = repository;
        _configuration = configuration;
        _logger = logger;
        _modelName = _configuration["Gemini:IntentModelName"]
            ?? _configuration["IntentClassification:ModelName"]
            ?? "gemini-2.5-flash-lite";
        _requestOptions = GeminiUtilityRequestOptions.FromConfiguration(_configuration);
    }

    /// <inheritdoc/>
    public async Task<IntentClassificationResult> ClassifyIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Classifying intent for message: {Message}", message);

        var instructions = await _repository.GetActiveByTopicsAsync(["intent-classification"], cancellationToken);
        var systemInstruction = instructions.FirstOrDefault()?.PersonaDefinition
            ?? "You are an intent classifier. Return JSON with intent, confidence, and additionalTopics.";

        var request = new GeminiRequest
        {
            ModelName = _modelName,
            SystemInstruction = systemInstruction,
            Messages = new List<GeminiMessage>
            {
                new GeminiMessage { Role = "user", Content = message }
            },
            Temperature = 0.1, // Low temperature for deterministic classification
            ThinkingBudget = 0,
            MaxTokens = 256,
            MaxPromptTokens = MaxClassificationPromptTokens,
            ResponseMimeType = "application/json",
            ResponseSchema = IntentClassificationSchema,
            ServiceTier = _requestOptions.ServiceTier,
            TimeoutSeconds = _requestOptions.TimeoutSeconds,
            Store = false
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
