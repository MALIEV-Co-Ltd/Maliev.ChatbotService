using Maliev.ChatbotService.Application.Models;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Client interface for Gemini API operations.
/// </summary>
public interface IGeminiClient
{
    /// <summary>
    /// Sends a message to the Gemini API.
    /// </summary>
    /// <param name="request">The request containing the message and context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the Gemini API.</returns>
    Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a response from the Gemini API.
    /// </summary>
    /// <param name="request">The request containing the message and context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Incremental response events from Gemini.</returns>
    IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request model for Gemini API.
/// </summary>
public class GeminiRequest
{
    /// <summary>
    /// Recommended timeout window for Gemini Flex inference requests, in seconds.
    /// </summary>
    public const int FlexInferenceTimeoutSeconds = 600;

    /// <summary>
    /// Gets or sets the model name to use for this request (optional).
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Gets or sets the system instructions.
    /// </summary>
    public string SystemInstruction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the conversation history.
    /// </summary>
    public List<GeminiMessage> Messages { get; set; } = new();

    /// <summary>
    /// Gets or sets the timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the prompt text for simple requests.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of tokens to generate.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed prompt token count before generation is skipped.
    /// When set, Gemini requests preflight with countTokens to avoid expensive oversized calls.
    /// </summary>
    public int? MaxPromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the temperature for response generation (0.0-2.0).
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the Gemini thinking token budget. Set to 0 to disable thinking for low-cost deterministic calls.
    /// </summary>
    public int? ThinkingBudget { get; set; }

    /// <summary>
    /// Gets or sets the Gemini media resolution for multimodal inputs.
    /// Use values such as MEDIA_RESOLUTION_LOW, MEDIA_RESOLUTION_MEDIUM, or MEDIA_RESOLUTION_HIGH.
    /// </summary>
    public string? MediaResolution { get; set; }

    /// <summary>
    /// Gets or sets the Gemini cached content resource name to use as a prompt prefix.
    /// </summary>
    public string? CachedContentName { get; set; }

    /// <summary>
    /// Gets or sets the Gemini service tier for this request.
    /// Use "flex" only for latency-tolerant work; omit for standard interactive requests.
    /// </summary>
    public string? ServiceTier { get; set; }

    /// <summary>
    /// Gets or sets whether Gemini may store this request for provider-side logging and datasets.
    /// Set to <see langword="false" /> for customer or private payloads.
    /// </summary>
    public bool? Store { get; set; }

    /// <summary>
    /// Gets or sets Gemini safety settings. When set, these override configured provider defaults.
    /// </summary>
    public List<GeminiSafetySetting>? SafetySettings { get; set; }

    /// <summary>
    /// Gets or sets the timeout for the request.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the image URL for multimodal requests.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets whether to enable web search capabilities.
    /// </summary>
    public bool EnableWebSearch { get; set; }

    /// <summary>
    /// Gets or sets whether this turn must fail closed when Google Search grounding is unavailable.
    /// This is an orchestration hint and is not serialized to the model provider.
    /// </summary>
    public bool RequireGrounding { get; set; }

    /// <summary>
    /// Gets or sets previously persisted, customer-safe grounding that may satisfy a continuation
    /// turn without repeating the external search. This is never serialized to the model provider.
    /// </summary>
    public GroundingProvenance? PriorGroundingProvenance { get; set; }

    /// <summary>
    /// Gets or sets whether to enable Gemini URL Context for URLs supplied in the prompt.
    /// </summary>
    public bool EnableUrlContext { get; set; }

    /// <summary>
    /// Gets or sets whether to request model thinking/reasoning blocks.
    /// </summary>
    public bool IncludeThoughts { get; set; }

    /// <summary>
    /// Gets or sets attachments for multimodal requests.
    /// </summary>
    public List<GeminiAttachment>? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the response MIME type for structured output (e.g., "application/json").
    /// </summary>
    public string? ResponseMimeType { get; set; }

    /// <summary>
    /// Gets or sets the JSON Schema object for structured output.
    /// When set alongside ResponseMimeType, Gemini guarantees the response matches this schema.
    /// </summary>
    public object? ResponseSchema { get; set; }

    /// <summary>
    /// Gets or sets the tool declarations for function calling.
    /// </summary>
    public List<GeminiToolDeclaration>? Tools { get; set; }

    /// <summary>
    /// Gets or sets the function calling configuration.
    /// </summary>
    public GeminiFunctionCallingConfig? ToolConfig { get; set; }
}

/// <summary>
/// Message model for Gemini API.
/// </summary>
public class GeminiMessage
{
    /// <summary>
    /// Gets or sets the role of the message sender (user, assistant, system).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets attachments for this specific message.
    /// </summary>
    public List<GeminiAttachment>? Attachments { get; set; }

    /// <summary>
    /// When set, this message represents a model turn that issued one or more tool calls. Providers
    /// must serialize these as native function-call parts (Gemini <c>functionCall</c> / OpenAI
    /// <c>tool_calls</c>) rather than as plain text.
    /// </summary>
    public List<GeminiFunctionCall>? FunctionCalls { get; set; }

    /// <summary>
    /// When set, this message represents a turn that returns tool results. Providers must serialize
    /// these as native function-response parts (Gemini <c>functionResponse</c> / OpenAI <c>tool</c>
    /// role messages) rather than as plain text.
    /// </summary>
    public List<GeminiFunctionResponse>? FunctionResponses { get; set; }
}

/// <summary>
/// Gemini safety setting for a single harm category.
/// </summary>
public sealed class GeminiSafetySetting
{
    /// <summary>
    /// Gets or sets the Gemini harm category, such as <c>HARM_CATEGORY_HARASSMENT</c>.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Gemini harm block threshold, such as <c>BLOCK_ONLY_HIGH</c>.
    /// </summary>
    public string Threshold { get; set; } = string.Empty;
}

/// <summary>
/// Response model from Gemini API.
/// </summary>
public class GeminiResponse
{
    /// <summary>
    /// Gets or sets the generated response text.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the request was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if unsuccessful.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the token usage metadata.
    /// </summary>
    public GeminiTokenUsage? TokenUsage { get; set; }

    /// <summary>
    /// Gets or sets the Gemini response service tier reported by the provider, when available.
    /// </summary>
    public string? ServiceTier { get; set; }

    /// <summary>
    /// Gets or sets Gemini Google Search grounding queries reported by the provider.
    /// </summary>
    public List<string> GroundingWebSearchQueries { get; set; } = [];

    /// <summary>
    /// Gets or sets customer-safe HTTPS source provenance reported by Google Search grounding.
    /// </summary>
    public List<GeminiGroundingSource> GroundingSources { get; set; } = [];

    /// <summary>
    /// Gets or sets customer-safe grounding provenance assembled by the agent harness.
    /// </summary>
    public GroundingProvenance? GroundingProvenance { get; set; }

    /// <summary>
    /// Gets or sets the number of Gemini prompts that used Google Search grounding.
    /// </summary>
    public int GoogleSearchGroundingPromptCount { get; set; }

    /// <summary>
    /// Gets or sets the error type if unsuccessful.
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// Gets or sets whether this response is a fallback response due to error.
    /// </summary>
    public bool IsFallback { get; set; }

    /// <summary>
    /// Gets or sets the accumulated thought/reasoning text from the model.
    /// </summary>
    public string? ThoughtContent { get; set; }

    /// <summary>
    /// Gets or sets the function calls from the response.
    /// </summary>
    public List<GeminiFunctionCall> FunctionCalls { get; set; } = new();

    /// <summary>
    /// Gets whether the response contains function calls.
    /// </summary>
    public bool HasFunctionCalls => FunctionCalls.Count > 0;
}

/// <summary>
/// Incremental Gemini streaming event.
/// </summary>
public class GeminiStreamEvent
{
    /// <summary>
    /// Gets or sets the event type: started, delta, final, or error.
    /// </summary>
    public string Type { get; set; } = "delta";

    /// <summary>
    /// Gets or sets the incremental generated text.
    /// </summary>
    public string? Delta { get; set; }

    /// <summary>
    /// Gets or sets incremental thought text (for thought-type events).
    /// </summary>
    public string? Thought { get; set; }

    /// <summary>
    /// Gets or sets the final accumulated response.
    /// </summary>
    public GeminiResponse? Response { get; set; }

    /// <summary>
    /// Gets or sets a customer-safe error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Token usage metadata from Gemini API.
/// </summary>
public class GeminiTokenUsage
{
    /// <summary>
    /// Gets or sets the number of prompt tokens.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of completion tokens.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of cached prompt tokens reported by Gemini.
    /// </summary>
    public int CachedPromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of tool-use prompt tokens reported by Gemini.
    /// </summary>
    public int ToolUsePromptTokens { get; set; }

    /// <summary>
    /// Gets or sets the number of thought tokens reported by Gemini.
    /// </summary>
    public int ThoughtTokens { get; set; }

    /// <summary>
    /// Gets or sets the total number of tokens.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets token counts by input modality for the effective prompt.
    /// </summary>
    public List<GeminiModalityTokenCount> PromptTokenDetails { get; set; } = [];

    /// <summary>
    /// Gets or sets token counts by cached input modality.
    /// </summary>
    public List<GeminiModalityTokenCount> CachedTokenDetails { get; set; } = [];

    /// <summary>
    /// Gets or sets token counts by generated candidate modality.
    /// </summary>
    public List<GeminiModalityTokenCount> CandidateTokenDetails { get; set; } = [];

    /// <summary>
    /// Gets or sets token counts by tool-use prompt modality.
    /// </summary>
    public List<GeminiModalityTokenCount> ToolUsePromptTokenDetails { get; set; } = [];
}

/// <summary>
/// A bounded customer-safe source returned by Gemini Google Search grounding.
/// </summary>
public sealed class GeminiGroundingSource
{
    /// <summary>Gets or sets the source title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the canonical HTTPS source URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized source host.</summary>
    public string Domain { get; set; } = string.Empty;
}

/// <summary>
/// Token count reported by Gemini for a single modality.
/// </summary>
public class GeminiModalityTokenCount
{
    /// <summary>
    /// Gets or sets the Gemini modality name, such as TEXT, IMAGE, VIDEO, AUDIO, or DOCUMENT.
    /// </summary>
    public string Modality { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token count for the modality.
    /// </summary>
    public int TokenCount { get; set; }
}

/// <summary>
/// Attachment model for multimodal Gemini requests.
/// </summary>
public class GeminiAttachment
{
    /// <summary>
    /// Gets or sets the content type (Image, PDF, Video, Audio).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data URL or file path.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;
}

/// <summary>
/// Tool declaration for Gemini function calling.
/// </summary>
public class GeminiToolDeclaration
{
    /// <summary>
    /// Gets or sets the function declarations in this tool.
    /// </summary>
    public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new();
}

/// <summary>
/// Individual function declaration for Gemini tools.
/// </summary>
public class GeminiFunctionDeclaration
{
    /// <summary>
    /// Gets or sets the function name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the function description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters schema (JSON Schema object).
    /// </summary>
    public object? Parameters { get; set; }
}

/// <summary>
/// Function calling configuration for Gemini.
/// </summary>
public class GeminiFunctionCallingConfig
{
    /// <summary>
    /// Gets or sets the function calling mode (AUTO, ANY, NONE).
    /// </summary>
    public string Mode { get; set; } = "AUTO";
}

/// <summary>
/// Represents a function call from Gemini response.
/// </summary>
public class GeminiFunctionCall
{
    /// <summary>
    /// Gets or sets the function name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the function arguments.
    /// </summary>
    public Dictionary<string, object> Args { get; set; } = new();

    /// <summary>
    /// Gets or sets the provider-assigned call id. Gemini 3.x and OpenAI require the matching id to
    /// be echoed back on the corresponding function response; may be null for models that omit it.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets Gemini's opaque thought signature for this function-call part. When present, it
    /// must be sent back on the same function-call part in subsequent manual REST history.
    /// </summary>
    public string? ThoughtSignature { get; set; }
}

/// <summary>
/// Represents a tool result to send back to the model as a native function-response part.
/// </summary>
public class GeminiFunctionResponse
{
    /// <summary>Gets or sets the name of the function that produced this result.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the matching call id, when the model provided one.</summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the raw tool result as a JSON string. Providers parse this into a JSON object
    /// for the response field; non-object JSON is wrapped as <c>{ "result": ... }</c>.
    /// </summary>
    public string ResponseJson { get; set; } = string.Empty;
}
