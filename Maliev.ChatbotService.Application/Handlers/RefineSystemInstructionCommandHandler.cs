using System.Text.Json;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handles LLM-assisted improvements to chatbot system instruction drafts.
/// </summary>
public class RefineSystemInstructionCommandHandler
{
    private const int InstructionTextMaxLength = 5000;
    private const int MaxRefinementOutputTokens = 4096;
    private const int MaxRefinementPromptTokens = 16000;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<RefineSystemInstructionCommandHandler> _logger;
    private readonly string _modelName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly object ResponseSchema = new
    {
        type = "object",
        properties = new
        {
            persona_definition = new { type = "string", description = "Improved persona or topic skill prompt body." },
            business_constraints = new { type = "string", description = "Improved safety, scope, and business guardrails." },
            summary = new { type = "string", description = "Brief summary of the improvements made." }
        },
        required = new[] { "persona_definition", "business_constraints", "summary" }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="RefineSystemInstructionCommandHandler"/> class.
    /// </summary>
    /// <param name="geminiClient">The Gemini API client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">Application configuration.</param>
    public RefineSystemInstructionCommandHandler(
        IGeminiClient geminiClient,
        ILogger<RefineSystemInstructionCommandHandler> logger,
        IConfiguration? configuration = null)
    {
        _geminiClient = geminiClient;
        _logger = logger;
        _modelName = configuration?["Gemini:IntentModelName"] ?? "gemini-2.5-flash-lite";
    }

    /// <summary>
    /// Improves the provided instruction draft and returns the revised fields without saving them.
    /// </summary>
    /// <param name="command">The instruction refinement command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refined instruction draft.</returns>
    public async Task<RefinedSystemInstructionResult> HandleAsync(
        RefineSystemInstructionCommand command,
        CancellationToken cancellationToken = default)
    {
        var request = new GeminiRequest
        {
            ModelName = _modelName,
            SystemInstruction = BuildSystemInstruction(),
            Messages =
            [
                new GeminiMessage
                {
                    Role = "user",
                    Content = BuildUserPrompt(command)
                }
            ],
            ResponseMimeType = "application/json",
            ResponseSchema = ResponseSchema,
            Temperature = 0.2,
            ThinkingBudget = 0,
            MaxTokens = MaxRefinementOutputTokens,
            MaxPromptTokens = MaxRefinementPromptTokens,
            ServiceTier = "flex",
            TimeoutSeconds = GeminiRequest.FlexInferenceTimeoutSeconds
        };

        var response = await _geminiClient.SendMessageAsync(request, cancellationToken);
        if (!response.Success)
        {
            _logger.LogWarning("System instruction refinement failed: {Error}", response.ErrorMessage);
            throw new InvalidOperationException(response.ErrorMessage ?? "AI instruction refinement failed.");
        }

        try
        {
            var refined = JsonSerializer.Deserialize<RefinedSystemInstructionResult>(response.Content, JsonOptions);
            if (refined is null)
            {
                throw new JsonException("AI response was empty.");
            }

            refined.PersonaDefinition = LimitText(refined.PersonaDefinition, command.PersonaDefinition);
            refined.BusinessConstraints = LimitText(refined.BusinessConstraints, command.BusinessConstraints);
            refined.Summary = string.IsNullOrWhiteSpace(refined.Summary)
                ? "Refined instruction clarity, scope, and guardrails."
                : refined.Summary.Trim();

            return refined;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse system instruction refinement response: {Content}", response.Content);
            throw new InvalidOperationException("AI instruction refinement returned an invalid response.", ex);
        }
    }

    private static string BuildSystemInstruction()
    {
        return """
            You are the MALIEV system instruction refiner for chatbot administrators.
            Improve chatbot system instructions and topic skills without changing the intended product scope.
            Keep instructions direct, operational, customer-safe, and production-ready.
            Preserve names, profile keys, business facts, safety requirements, and channel boundaries.
            Do not add secrets, credentials, internal policy details, hidden chain-of-thought requirements, or unverifiable claims.
            Return only JSON that matches the requested schema.
            """;
    }

    private static string BuildUserPrompt(RefineSystemInstructionCommand command)
    {
        return $"""
            Improve this MALIEV chatbot instruction draft.

            Name: {command.Name}
            Category: {command.Category}
            Topic/Profile key: {command.TopicKey ?? "(none)"}
            Improvement goal: {command.ImprovementGoal ?? "Make the instruction clearer, more concise, safer, and easier for the chatbot to follow."}

            System prompt / skill instructions:
            {command.PersonaDefinition}

            Business constraints / guardrails:
            {command.BusinessConstraints}

            Requirements:
            - Return improved persona_definition and business_constraints as separate fields.
            - Keep each field under 5,000 characters.
            - Preserve the original language unless clarity requires short English labels.
            - Make the instruction specific enough for a manufacturing assistant, not generic chatbot advice.
            - Do not save anything. This is only a draft for an administrator to review.
            """;
    }

    private static string LimitText(string text, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        return normalized.Length <= InstructionTextMaxLength
            ? normalized
            : normalized[..InstructionTextMaxLength].Trim();
    }
}
