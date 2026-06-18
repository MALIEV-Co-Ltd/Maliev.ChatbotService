using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Cleans up raw dictated speech text using Gemini directly, bypassing the agent pipeline.
/// </summary>
public class CleanDictationSpeechCommandHandler
{
    private const string SystemInstruction =
        "You are a speech transcript cleanup assistant. Remove filler words, fix punctuation, " +
        "ensure proper capitalization, and return ONLY the cleaned text. " +
        "Do not add any explanation, prefix, or commentary.";

    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<CleanDictationSpeechCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanDictationSpeechCommandHandler"/> class.
    /// </summary>
    public CleanDictationSpeechCommandHandler(IGeminiClient geminiClient, ILogger<CleanDictationSpeechCommandHandler> logger)
    {
        _geminiClient = geminiClient;
        _logger = logger;
    }

    /// <summary>
    /// Handles the speech cleanup command.
    /// </summary>
    public async Task<CleanDictationSpeechResult> HandleAsync(CleanDictationSpeechCommand command, CancellationToken cancellationToken = default)
    {
        var request = new GeminiRequest
        {
            ModelName = "gemini-2.5-flash-lite",
            SystemInstruction = SystemInstruction,
            Messages = new List<GeminiMessage>
            {
                new() { Role = "user", Content = command.Speech }
            },
            Temperature = 0.1,
            TimeoutSeconds = 5,
        };

        try
        {
            var response = await _geminiClient.SendMessageAsync(request, cancellationToken);
            if (!response.Success)
            {
                _logger.LogWarning("Speech cleanup failed: {Error}", response.ErrorMessage);
                return new CleanDictationSpeechResult { Success = false, ErrorMessage = response.ErrorMessage };
            }

            return new CleanDictationSpeechResult { Success = true, CleanedText = response.Content.Trim() };
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
}
