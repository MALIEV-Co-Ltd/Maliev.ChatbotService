using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Implementation of the Gemini API client using HttpClient.
/// </summary>
public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiClient> _logger;
    private readonly ConversationMetrics _metrics;
    private readonly string _apiKey;
    private readonly string _modelName;

    private int _totalApiCalls;
    private int _successfulApiCalls;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="metrics">The conversation metrics.</param>
    /// <param name="logger">The logger.</param>
    public GeminiClient(HttpClient httpClient, IConfiguration configuration, ConversationMetrics metrics, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _metrics = metrics;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
        _modelName = configuration["Gemini:MainModelName"] ?? "gemini-2.5-flash";
        _totalApiCalls = 0;
        _successfulApiCalls = 0;
    }

    /// <inheritdoc/>
    public async Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalApiCalls);

        try
        {
            var modelName = request.ModelName ?? _modelName;
            var url = $"v1beta/models/{modelName}:generateContent";

            // Build contents with multimodal support
            var contentsParts = new List<object>();

            foreach (var message in request.Messages)
            {
                var messageParts = new List<object>
                {
                    new { text = message.Content }
                };

                if (message.Attachments != null)
                {
                    foreach (var attachment in message.Attachments)
                    {
                        messageParts.Add(GetAttachmentPart(attachment));
                    }
                }

                contentsParts.Add(new
                {
                    role = message.Role == "assistant" ? "model" : "user",
                    parts = messageParts.ToArray()
                });
            }

            // Add top-level request attachments to the last user message if present (legacy support)
            if (request.Attachments != null && request.Attachments.Count > 0 && contentsParts.Count > 0)
            {
                var lastPart = contentsParts.Last();
                // This is a bit complex due to anonymous types, so we'll just handle it by ensuring 
                // we don't duplicate if possible. For simplicity in this refactor, we prefer message-level attachments.
                // But to maintain compatibility:
                var lastMessage = request.Messages.Last();
                if (lastMessage.Role != "assistant")
                {
                    var attachmentParts = request.Attachments.Select(GetAttachmentPart).ToList();
                    
                    // Re-create the last entry with merged parts
                    var existingParts = new List<object> { new { text = lastMessage.Content } };
                    if (lastMessage.Attachments != null)
                    {
                        existingParts.AddRange(lastMessage.Attachments.Select(GetAttachmentPart));
                    }
                    existingParts.AddRange(attachmentParts);

                    contentsParts[contentsParts.Count - 1] = new
                    {
                        role = "user",
                        parts = existingParts.ToArray()
                    };
                }
            }

            object geminiPayload;
            var hasTools = request.Tools != null && request.Tools.Count > 0;

            if (!string.IsNullOrEmpty(request.ResponseMimeType))
            {
                geminiPayload = new
                {
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = request.SystemInstruction }
                        }
                    },
                    contents = contentsParts.ToArray(),
                    generationConfig = new
                    {
                        responseMimeType = request.ResponseMimeType,
                        responseSchema = request.ResponseSchema
                    }
                };
            }
            else if (hasTools)
            {
                geminiPayload = new
                {
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = request.SystemInstruction }
                        }
                    },
                    contents = contentsParts.ToArray(),
                    tools = request.Tools!.Select(t => new
                    {
                        functionDeclarations = t.FunctionDeclarations.Select(f => new
                        {
                            name = f.Name,
                            description = f.Description,
                            parameters = f.Parameters
                        })
                    }),
                    toolConfig = new
                    {
                        functionCallingConfig = new { mode = request.ToolConfig?.Mode ?? "AUTO" }
                    }
                };
            }
            else
            {
                geminiPayload = new
                {
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = request.SystemInstruction }
                        }
                    },
                    contents = contentsParts.ToArray()
                };
            }

            var json = JsonSerializer.Serialize(geminiPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var messageRequest = new HttpRequestMessage(HttpMethod.Post, url);
            messageRequest.Headers.Add("x-goog-api-key", _apiKey);
            messageRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            var response = await _httpClient.SendAsync(messageRequest, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Gemini API rate limit exceeded (429)");
                    UpdateSuccessRate();
                    return GetFallbackResponse("GeminiAPIRateLimit");
                }

                _logger.LogError("Gemini API returned error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                UpdateSuccessRate();
                return GetFallbackResponse("GeminiAPIError");
            }

            var geminiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var candidates = geminiResponse.GetProperty("candidates");
            var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
            
            // Check if generation was blocked
            if (firstCandidate.TryGetProperty("finishReason", out var finishReason) && 
                finishReason.GetString() == "SAFETY")
            {
                _logger.LogWarning("Gemini API blocked response due to safety filters");
                UpdateSuccessRate();
                return GetFallbackResponse("ValidationFailure");
            }

            var contentProp = firstCandidate.GetProperty("content");
            var parts = contentProp.GetProperty("parts");

            // Parse all parts — may contain text and/or function calls
            var textParts = new List<string>();
            var functionCalls = new List<GeminiFunctionCall>();

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textProp))
                {
                    textParts.Add(textProp.GetString() ?? string.Empty);
                }
                else if (part.TryGetProperty("functionCall", out var fcProp))
                {
                    var fc = new GeminiFunctionCall
                    {
                        Name = fcProp.GetProperty("name").GetString() ?? string.Empty,
                        Args = new Dictionary<string, object>()
                    };
                    if (fcProp.TryGetProperty("args", out var argsProp))
                    {
                        foreach (var arg in argsProp.EnumerateObject())
                        {
                            fc.Args[arg.Name] = arg.Value.ValueKind switch
                            {
                                JsonValueKind.String => arg.Value.GetString()!,
                                JsonValueKind.Number => arg.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => arg.Value.GetRawText()
                            };
                        }
                    }
                    functionCalls.Add(fc);
                }
            }

            var text = string.Join("", textParts);

            // Extract token usage if available
            GeminiTokenUsage? tokenUsage = null;
            if (geminiResponse.TryGetProperty("usageMetadata", out var usageMetadata))
            {
                tokenUsage = new GeminiTokenUsage
                {
                    PromptTokens = usageMetadata.TryGetProperty("promptTokenCount", out var promptTokens) ? promptTokens.GetInt32() : 0,
                    CompletionTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out var completionTokens) ? completionTokens.GetInt32() : 0,
                    TotalTokens = usageMetadata.TryGetProperty("totalTokenCount", out var totalTokens) ? totalTokens.GetInt32() : 0
                };
            }

            Interlocked.Increment(ref _successfulApiCalls);
            UpdateSuccessRate();

            return new GeminiResponse
            {
                Success = true,
                Content = text,
                TokenUsage = tokenUsage,
                FunctionCalls = functionCalls
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Gemini API request timed out after {Timeout} seconds", request.TimeoutSeconds);
            UpdateSuccessRate();
            return GetFallbackResponse("GeminiAPITimeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            UpdateSuccessRate();
            return GetFallbackResponse("UnexpectedError");
        }
    }

    private static object GetAttachmentPart(GeminiAttachment attachment)
    {
        if (attachment.Data.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            attachment.Data.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                fileData = new
                {
                    fileUri = attachment.Data,
                    mimeType = attachment.MimeType
                }
            };
        }
        else
        {
            // Base64-encoded data (images, PDFs, videos, audio)
            var base64Data = attachment.Data.Contains(',')
                ? attachment.Data.Split(',')[1]
                : attachment.Data;

            return new
            {
                inlineData = new
                {
                    data = base64Data,
                    mimeType = attachment.MimeType
                }
            };
        }
    }

    private void UpdateSuccessRate()
    {
        var totalCalls = _totalApiCalls;
        var successfulCalls = _successfulApiCalls;

        if (totalCalls > 0)
        {
            var successRate = (double)successfulCalls / totalCalls;
            _metrics.UpdateGeminiApiSuccessRate(successRate);
        }
    }

    private GeminiResponse GetFallbackResponse(string errorType)
    {
        var fallbackMessages = new Dictionary<string, string>
        {
            ["GeminiAPITimeout"] = "I apologize, but I'm experiencing delays in processing your request. Please try again in a few moments or contact our team at info@maliev.com.",
            ["GeminiAPIError"] = "I apologize, but I'm temporarily unable to process your request. Please try again in a few moments or contact our team at info@maliev.com.",
            ["GeminiAPIRateLimit"] = "We have exceeded the AI processing limit for today. Please wait a few minutes before trying again, or fill in the information manually. If this persists, please contact support at info@maliev.com.",
            ["RedisUnavailable"] = "I'm currently experiencing slower response times, but I'm still here to help. Please bear with me.",
            ["ValidationFailure"] = "I apologize, but I'm having trouble formulating a proper response. Could you please rephrase your question?",
            ["UnexpectedError"] = "I apologize for the inconvenience. Something unexpected occurred. Please try again, or contact our support team at info@maliev.com."
        };

        var message = fallbackMessages.ContainsKey(errorType)
            ? fallbackMessages[errorType]
            : fallbackMessages["UnexpectedError"];

        _logger.LogWarning("Returning fallback response for error type: {ErrorType}", errorType);

        return new GeminiResponse
        {
            Success = false,
            Content = message,
            ErrorMessage = message,
            ErrorType = errorType,
            IsFallback = true
        };
    }
}
