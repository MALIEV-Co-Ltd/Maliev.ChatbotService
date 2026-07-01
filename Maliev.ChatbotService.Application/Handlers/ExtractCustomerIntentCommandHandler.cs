using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for extracting customer intent from user messages using Gemini structured output.
/// </summary>
public class ExtractCustomerIntentCommandHandler
{
    private const int MaxIntentOutputTokens = 128;
    private const int MaxIntentPromptTokens = 4096;

    private readonly IGeminiClient _geminiClient;
    private readonly ISystemInstructionRepository _instructionRepository;
    private readonly ILogger<ExtractCustomerIntentCommandHandler> _logger;
    private readonly string _modelName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractCustomerIntentCommandHandler"/> class.
    /// </summary>
    public ExtractCustomerIntentCommandHandler(
        IGeminiClient geminiClient,
        ISystemInstructionRepository instructionRepository,
        ILogger<ExtractCustomerIntentCommandHandler> logger,
        IConfiguration? configuration = null)
    {
        _geminiClient = geminiClient;
        _instructionRepository = instructionRepository;
        _logger = logger;
        _modelName = configuration?["Gemini:IntentModelName"] ?? "gemini-2.5-flash-lite";
    }

    /// <summary>
    /// Handles the customer intent extraction command.
    /// </summary>
    public async Task<ExtractCustomerIntentResult> HandleAsync(ExtractCustomerIntentCommand command, CancellationToken cancellationToken = default)
    {
        var instructions = await _instructionRepository.GetActiveByTopicsAsync(["customer-intent-extraction"], cancellationToken);
        var systemInstruction = instructions.FirstOrDefault()?.PersonaDefinition
            ?? "Analyze the user message and determine if they need customer data. Return JSON with needs_customer_data (bool), customer_search_term (string or null), needs_history (bool).";

        var schema = new
        {
            type = "object",
            properties = new
            {
                needs_customer_data = new { type = "boolean", description = "True if the user is asking about a specific customer's data" },
                customer_search_term = new { type = "string", description = "The customer name, email, or identifier to search for. Null if not specified" },
                needs_history = new { type = "boolean", description = "True if the user is asking about changes, updates, or audit history" }
            },
            required = new[] { "needs_customer_data", "customer_search_term", "needs_history" }
        };

        var request = new GeminiRequest
        {
            ModelName = _modelName,
            SystemInstruction = systemInstruction,
            Messages = new List<GeminiMessage>
            {
                new() { Role = "user", Content = command.UserMessage }
            },
            Temperature = 0.1,
            ThinkingBudget = 0,
            MaxTokens = MaxIntentOutputTokens,
            MaxPromptTokens = MaxIntentPromptTokens,
            TimeoutSeconds = 5,
            ResponseMimeType = "application/json",
            ResponseSchema = schema
        };

        try
        {
            var response = await _geminiClient.SendMessageAsync(request, cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("Customer intent extraction failed: {Error}", response.ErrorMessage);
                return new ExtractCustomerIntentResult { Success = false, ErrorMessage = response.ErrorMessage };
            }

            var result = JsonSerializer.Deserialize<IntentJsonResult>(response.Content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                _logger.LogWarning("Failed to deserialize customer intent result: {Content}", response.Content);
                return new ExtractCustomerIntentResult { Success = false, ErrorMessage = "Failed to parse AI response" };
            }

            return new ExtractCustomerIntentResult
            {
                Success = true,
                NeedsCustomerData = result.NeedsCustomerData,
                CustomerSearchTerm = result.CustomerSearchTerm,
                NeedsHistory = result.NeedsHistory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during customer intent extraction");
            return new ExtractCustomerIntentResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private record IntentJsonResult(
        [property: System.Text.Json.Serialization.JsonPropertyName("needs_customer_data")] bool NeedsCustomerData,
        [property: System.Text.Json.Serialization.JsonPropertyName("customer_search_term")] string? CustomerSearchTerm,
        [property: System.Text.Json.Serialization.JsonPropertyName("needs_history")] bool NeedsHistory);
}

/// <summary>
/// Result of customer intent extraction.
/// </summary>
public class ExtractCustomerIntentResult
{
    /// <summary>Whether the extraction succeeded.</summary>
    public bool Success { get; set; }
    /// <summary>Error message if the extraction failed.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Whether the user needs customer data.</summary>
    public bool NeedsCustomerData { get; set; }
    /// <summary>The extracted customer search term.</summary>
    public string? CustomerSearchTerm { get; set; }
    /// <summary>Whether the user needs activity history.</summary>
    public bool NeedsHistory { get; set; }
}
