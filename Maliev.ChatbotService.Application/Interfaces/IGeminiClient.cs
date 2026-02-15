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
}

/// <summary>
/// Request model for Gemini API.
/// </summary>
public class GeminiRequest
{
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
    /// Gets or sets the temperature for response generation (0.0-2.0).
    /// </summary>
    public double? Temperature { get; set; }

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
    /// Gets or sets the error type if unsuccessful.
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// Gets or sets whether this response is a fallback response due to error.
    /// </summary>
    public bool IsFallback { get; set; }

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
    /// Gets or sets the total number of tokens.
    /// </summary>
    public int TotalTokens { get; set; }
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
}
