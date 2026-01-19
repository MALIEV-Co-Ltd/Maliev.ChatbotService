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

    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = { 1000, 2000, 4000 }; // Exponential backoff

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

        // Attempt request with exponential backoff retry
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var modelName = request.ModelName ?? _modelName;
                var url = $"v1beta/models/{modelName}:generateContent";

                // ... (build contents logic same as before)
                // Build contents with multimodal support
                var contentsParts = new List<object>();

                foreach (var message in request.Messages)
                {
                    contentsParts.Add(new
                    {
                        role = message.Role == "assistant" ? "model" : "user",
                        parts = new object[] { new { text = message.Content } }
                    });
                }

                // Add attachments to the last user message if present
                if (request.Attachments != null && request.Attachments.Count > 0)
                {
                    var attachmentParts = new List<object>();

                    foreach (var attachment in request.Attachments)
                    {
                        if (attachment.ContentType.Equals("image", StringComparison.OrdinalIgnoreCase))
                        {
                            if (attachment.Data.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                // URL-based image
                                attachmentParts.Add(new
                                {
                                    fileData = new
                                    {
                                        fileUri = attachment.Data,
                                        mimeType = attachment.MimeType
                                    }
                                });
                            }
                            else
                            {
                                // Base64-encoded image
                                var base64Data = attachment.Data.Contains(',')
                                    ? attachment.Data.Split(',')[1]
                                    : attachment.Data;

                                attachmentParts.Add(new
                                {
                                    inlineData = new
                                    {
                                        data = base64Data,
                                        mimeType = attachment.MimeType
                                    }
                                });
                            }
                        }
                        else
                        {
                            // For PDFs, videos, audio - use file URI
                            attachmentParts.Add(new
                            {
                                fileData = new
                                {
                                    fileUri = attachment.Data,
                                    mimeType = attachment.MimeType
                                }
                            });
                        }
                    }

                    // Replace last user message with one that includes attachments
                    if (attachmentParts.Count > 0 && contentsParts.Count > 0)
                    {
                        var combinedParts = new List<object>
                        {
                            new { text = request.Messages.Last().Content }
                        };
                        combinedParts.AddRange(attachmentParts);

                        contentsParts[contentsParts.Count - 1] = new
                        {
                            role = "user",
                            parts = combinedParts.ToArray()
                        };
                    }
                }

                var geminiPayload = new
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
                    // Check if it's a transient error worth retrying
                    var isTransient = response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                                     response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                     response.StatusCode == System.Net.HttpStatusCode.InternalServerError;

                    if (isTransient && attempt < MaxRetries - 1)
                    {
                        _logger.LogWarning("Gemini API returned transient error {StatusCode}, retrying in {DelayMs}ms (attempt {Attempt}/{MaxAttempts})",
                            response.StatusCode, RetryDelaysMs[attempt], attempt + 1, MaxRetries);
                        await Task.Delay(RetryDelaysMs[attempt], cancellationToken);
                        continue;
                    }

                    _logger.LogError("Gemini API returned error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    UpdateSuccessRate();
                    return GetFallbackResponse("GeminiAPIError");
                }

                var geminiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var candidates = geminiResponse.GetProperty("candidates");
                var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
                var contentProp = firstCandidate.GetProperty("content");
                var parts = contentProp.GetProperty("parts");
                var firstPart = parts.EnumerateArray().FirstOrDefault();
                var text = firstPart.GetProperty("text").GetString() ?? string.Empty;

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
                    TokenUsage = tokenUsage
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Gemini API request timed out after {Timeout} seconds (attempt {Attempt}/{MaxAttempts})",
                    request.TimeoutSeconds, attempt + 1, MaxRetries);

                // Don't retry on timeout - return fallback immediately
                UpdateSuccessRate();
                return GetFallbackResponse("GeminiAPITimeout");
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                _logger.LogWarning(ex, "Gemini API network error, retrying in {DelayMs}ms (attempt {Attempt}/{MaxAttempts})",
                    RetryDelaysMs[attempt], attempt + 1, MaxRetries);
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API (attempt {Attempt}/{MaxAttempts})", attempt + 1, MaxRetries);

                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(RetryDelaysMs[attempt], cancellationToken);
                    continue;
                }

                UpdateSuccessRate();
                return GetFallbackResponse("UnexpectedError");
            }
        }

        // All retries exhausted
        UpdateSuccessRate();
        return GetFallbackResponse("GeminiAPIError");
    }

    /// <summary>
    /// Updates the Gemini API success rate metric.
    /// </summary>
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

    /// <summary>
    /// Gets a predefined fallback response for error scenarios.
    /// </summary>
    /// <param name="errorType">The type of error that occurred.</param>
    /// <returns>A fallback response with appropriate messaging.</returns>
    private GeminiResponse GetFallbackResponse(string errorType)
    {
        var fallbackMessages = new Dictionary<string, string>
        {
            ["GeminiAPITimeout"] = "I apologize, but I'm experiencing delays in processing your request. Our team is available to assist you directly. Please contact us at info@maliev.com or call +66-2-123-4567.",
            ["GeminiAPIError"] = "I apologize, but I'm temporarily unable to process your request. Please try again in a few moments, or contact our team directly at info@maliev.com or call +66-2-123-4567.",
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
            Success = false, // Return false for fallback to avoid saving to history
            Content = message,
            IsFallback = true
        };
    }
}
