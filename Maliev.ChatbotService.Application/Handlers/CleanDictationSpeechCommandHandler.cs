using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Configuration;
using Maliev.ChatbotService.Application.Costing;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Cleans up raw dictated speech text using Gemini directly, bypassing the agent pipeline.
/// </summary>
public class CleanDictationSpeechCommandHandler
{
    private const int MaxCleanupOutputTokens = 1024;
    private const int MaxCleanupPromptTokens = 4096;

    private const string SystemInstruction =
        "You are a speech transcript cleanup assistant. Remove filler words, fix punctuation, " +
        "ensure proper capitalization, and return ONLY the cleaned text. " +
        "Do not add any explanation, prefix, or commentary.";

    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<CleanDictationSpeechCommandHandler> _logger;
    private readonly string _modelName;
    private readonly GeminiUtilityRequestOptions _requestOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanDictationSpeechCommandHandler"/> class.
    /// </summary>
    public CleanDictationSpeechCommandHandler(
        IGeminiClient geminiClient,
        ILogger<CleanDictationSpeechCommandHandler> logger,
        IConfiguration? configuration = null)
    {
        _geminiClient = geminiClient;
        _logger = logger;
        _modelName = configuration?["Gemini:IntentModelName"] ?? "gemini-2.5-flash-lite";
        _requestOptions = GeminiUtilityRequestOptions.FromConfiguration(configuration);
    }

    /// <summary>
    /// Handles the speech cleanup command.
    /// </summary>
    public async Task<CleanDictationSpeechResult> HandleAsync(CleanDictationSpeechCommand command, CancellationToken cancellationToken = default)
    {
        var request = new GeminiRequest
        {
            ModelName = _modelName,
            SystemInstruction = SystemInstruction,
            Messages = new List<GeminiMessage>
            {
                new() { Role = "user", Content = command.Speech }
            },
            Temperature = 0.1,
            ThinkingBudget = 0,
            MaxTokens = MaxCleanupOutputTokens,
            MaxPromptTokens = MaxCleanupPromptTokens,
            ServiceTier = _requestOptions.ServiceTier,
            TimeoutSeconds = _requestOptions.TimeoutSeconds,
        };

        try
        {
            var response = await _geminiClient.SendMessageAsync(request, cancellationToken);
            if (!response.Success)
            {
                _logger.LogWarning("Speech cleanup failed: {Error}", response.ErrorMessage);
                return new CleanDictationSpeechResult { Success = false, ErrorMessage = response.ErrorMessage };
            }

            return new CleanDictationSpeechResult
            {
                Success = true,
                CleanedText = response.Content.Trim(),
                TokenUsage = response.TokenUsage,
                CostEstimate = GeminiCostEstimator.Estimate(
                    _modelName,
                    response.ServiceTier ?? request.ServiceTier,
                    response.TokenUsage)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during speech cleanup");
            return new CleanDictationSpeechResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}

/// <summary>
/// Result of the dictation speech cleanup operation.
/// </summary>
public class CleanDictationSpeechResult
{
    /// <summary>Whether the cleanup succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the cleanup failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The cleaned speech text.</summary>
    public string CleanedText { get; set; } = string.Empty;

    /// <summary>Gemini token usage reported for the speech cleanup call.</summary>
    public GeminiTokenUsage? TokenUsage { get; set; }

    /// <summary>Estimated Gemini cost for the speech cleanup call.</summary>
    public GeminiCostEstimate? CostEstimate { get; set; }
}
