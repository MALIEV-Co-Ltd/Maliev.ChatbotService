using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Implementation of the Gemini API client using HttpClient.
/// </summary>
public class GeminiClient : IGeminiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiClient> _logger;
    private readonly ConversationMetrics _metrics;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly int _flexRetryMaxAttempts;
    private readonly TimeSpan _flexRetryBaseDelay;

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
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "Gemini API key is not configured. Set 'Gemini:ApiKey' in user secrets: " +
                "dotnet user-secrets set \"Gemini:ApiKey\" \"<your-key>\" --project Maliev.ChatbotService.Api");
        _apiKey = apiKey;
        _modelName = configuration["Gemini:MainModelName"] ?? "gemini-2.5-flash";
        _flexRetryMaxAttempts = Math.Clamp(configuration.GetValue<int?>("Gemini:FlexRetryMaxAttempts") ?? 3, 1, 5);
        _flexRetryBaseDelay = TimeSpan.FromMilliseconds(
            Math.Max(0, configuration.GetValue<double?>("Gemini:FlexRetryBaseDelayMs") ?? 5000));
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            var promptLimitResponse = await TryEnforcePromptTokenLimitAsync(request, modelName, cts.Token);
            if (promptLimitResponse is not null)
            {
                return promptLimitResponse;
            }

            var url = $"v1beta/models/{modelName}:generateContent";
            var json = BuildGeminiPayloadJson(request);
            var maxAttempts = ResolveMaxAttempts(request);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var messageRequest = new HttpRequestMessage(HttpMethod.Post, url);
                AddGeminiHeaders(messageRequest, request);
                messageRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(messageRequest, cts.Token);
                var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    if (ShouldRetryFlexFailure(request, response.StatusCode, attempt, maxAttempts))
                    {
                        var retryDelay = ResolveFlexRetryDelay(attempt);
                        _logger.LogWarning(
                            "Gemini Flex request returned {StatusCode} on attempt {Attempt}/{MaxAttempts}. Retrying as Flex after {DelayMs}ms.",
                            response.StatusCode,
                            attempt,
                            maxAttempts,
                            retryDelay.TotalMilliseconds);

                        if (retryDelay > TimeSpan.Zero)
                        {
                            await Task.Delay(retryDelay, cts.Token);
                        }

                        continue;
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("Gemini API rate limit exceeded (429)");
                        UpdateSuccessRate();
                        return GetFallbackResponse("GeminiAPIRateLimit");
                    }

                    _logger.LogError("Gemini API returned error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    UpdateSuccessRate();
                    return GetFallbackResponse("GeminiAPIError");
                }

                using var document = JsonDocument.Parse(responseContent);
                var parsed = ParseGeminiResponse(document.RootElement);
                if (!parsed.Success)
                {
                    return parsed;
                }

                Interlocked.Increment(ref _successfulApiCalls);
                UpdateSuccessRate();

                return parsed;
            }

            UpdateSuccessRate();
            return GetFallbackResponse("GeminiAPIError");
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

    /// <inheritdoc/>
    public async IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(
        GeminiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalApiCalls);
        yield return new GeminiStreamEvent { Type = "started" };

        var accumulatedText = new StringBuilder();
        var accumulatedThought = new StringBuilder();
        var functionCalls = new List<GeminiFunctionCall>();
        GeminiTokenUsage? tokenUsage = null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        var modelName = request.ModelName ?? _modelName;
        var promptLimitResponse = await TryEnforcePromptTokenLimitAsync(request, modelName, cts.Token);
        if (promptLimitResponse is not null)
        {
            yield return new GeminiStreamEvent
            {
                Type = "final",
                Response = promptLimitResponse
            };
            yield break;
        }

        var url = $"v1beta/models/{modelName}:streamGenerateContent?alt=sse";
        var json = BuildGeminiPayloadJson(request);

        using var messageRequest = new HttpRequestMessage(HttpMethod.Post, url);
        AddGeminiHeaders(messageRequest, request);
        messageRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(
            messageRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogError("Gemini streaming API returned error: {StatusCode} - {Content}", response.StatusCode, responseContent);
            UpdateSuccessRate();
            yield return new GeminiStreamEvent
            {
                Type = "final",
                Response = GetFallbackResponse(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? "GeminiAPIRateLimit"
                    : "GeminiAPIError")
            };
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0 || data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var document = JsonDocument.Parse(data);
            var parsed = ParseGeminiResponse(document.RootElement);
            if (!parsed.Success)
            {
                yield return new GeminiStreamEvent
                {
                    Type = "final",
                    Response = parsed
                };
                yield break;
            }

            if (!string.IsNullOrEmpty(parsed.Content))
            {
                accumulatedText.Append(parsed.Content);
                yield return new GeminiStreamEvent
                {
                    Type = "delta",
                    Delta = parsed.Content
                };
            }

            if (!string.IsNullOrEmpty(parsed.ThoughtContent))
            {
                accumulatedThought.Append(parsed.ThoughtContent);
                yield return new GeminiStreamEvent
                {
                    Type = "thought",
                    Thought = parsed.ThoughtContent
                };
            }

            if (parsed.FunctionCalls.Count > 0)
            {
                functionCalls.AddRange(parsed.FunctionCalls);
            }

            tokenUsage = parsed.TokenUsage ?? tokenUsage;
        }

        Interlocked.Increment(ref _successfulApiCalls);
        UpdateSuccessRate();
        yield return new GeminiStreamEvent
        {
            Type = "final",
            Response = new GeminiResponse
            {
                Success = true,
                Content = accumulatedText.ToString(),
                ThoughtContent = accumulatedThought.ToString(),
                FunctionCalls = functionCalls,
                TokenUsage = tokenUsage
            }
        };
    }

    private static object GetAttachmentPart(GeminiAttachment attachment)
    {
        if (IsGeminiFileUri(attachment.Data) || IsSupportedHttpsFileUrl(attachment.Data, attachment.MimeType))
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

        if (attachment.Data.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            attachment.Data.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                text = $"Attached file reference ({attachment.MimeType}): {attachment.Data}"
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

    private static bool IsGeminiFileUri(string data)
    {
        return data.StartsWith("gs://", StringComparison.OrdinalIgnoreCase) ||
            data.StartsWith("https://generativelanguage.googleapis.com/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedHttpsFileUrl(string data, string mimeType)
    {
        if (!data.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsSupportedExternalFileMimeType(mimeType);
    }

    private static bool IsSupportedExternalFileMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        var normalizedMimeType = mimeType.Trim();
        return normalizedMimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            normalizedMimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
            normalizedMimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            normalizedMimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            normalizedMimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            normalizedMimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private void AddGeminiHeaders(HttpRequestMessage messageRequest, GeminiRequest request)
    {
        messageRequest.Headers.Add("x-goog-api-key", _apiKey);
        if (string.Equals(request.ServiceTier, "flex", StringComparison.OrdinalIgnoreCase))
        {
            messageRequest.Headers.Add("X-Server-Timeout", request.TimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        }
    }

    private int ResolveMaxAttempts(GeminiRequest request) =>
        string.Equals(request.ServiceTier, "flex", StringComparison.OrdinalIgnoreCase)
            ? _flexRetryMaxAttempts
            : 1;

    private static bool ShouldRetryFlexFailure(
        GeminiRequest request,
        HttpStatusCode statusCode,
        int attempt,
        int maxAttempts)
    {
        if (!string.Equals(request.ServiceTier, "flex", StringComparison.OrdinalIgnoreCase) ||
            attempt >= maxAttempts)
        {
            return false;
        }

        return statusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests;
    }

    private TimeSpan ResolveFlexRetryDelay(int failedAttempt) =>
        TimeSpan.FromMilliseconds(_flexRetryBaseDelay.TotalMilliseconds * Math.Pow(2, failedAttempt - 1));

    /// <summary>
    /// Builds the Gemini <c>contents</c> array, emitting native functionCall/functionResponse parts
    /// for tool turns and text/media parts otherwise. Shared by the sync and streaming payloads.
    /// </summary>
    private static List<object> BuildContents(GeminiRequest request)
    {
        var messages = request.Messages.Count > 0
            ? request.Messages
            : BuildPromptMessages(request.Prompt);

        var contentsParts = new List<object>();
        foreach (var message in messages)
        {
            contentsParts.Add(BuildContentEntry(message));
        }

        // Legacy: merge top-level request attachments into the last plain-text user message.
        if (request.Attachments is { Count: > 0 } && contentsParts.Count > 0)
        {
            var lastMessage = messages[^1];
            if (lastMessage.Role != "assistant" &&
                lastMessage.FunctionCalls is null &&
                lastMessage.FunctionResponses is null)
            {
                var existingParts = new List<object> { new { text = lastMessage.Content } };
                if (lastMessage.Attachments != null)
                {
                    existingParts.AddRange(lastMessage.Attachments.Select(GetAttachmentPart));
                }

                existingParts.AddRange(request.Attachments.Select(GetAttachmentPart));
                contentsParts[^1] = new { role = "user", parts = existingParts.ToArray() };
            }
        }

        return contentsParts;
    }

    private static List<GeminiMessage> BuildPromptMessages(string? prompt) =>
        string.IsNullOrWhiteSpace(prompt)
            ? new List<GeminiMessage>()
            : new List<GeminiMessage> { new() { Role = "user", Content = prompt } };

    private static object BuildContentEntry(GeminiMessage message)
    {
        // Model turn that issued tool calls.
        if (message.FunctionCalls is { Count: > 0 })
        {
            var callParts = message.FunctionCalls
                .Select(fc => fc.Id is { Length: > 0 }
                    ? (object)new { functionCall = new { name = fc.Name, args = fc.Args, id = fc.Id } }
                    : new { functionCall = new { name = fc.Name, args = fc.Args } })
                .ToArray();
            return new { role = "model", parts = callParts };
        }

        // Turn that returns tool results.
        if (message.FunctionResponses is { Count: > 0 })
        {
            var responseParts = new List<object>();
            foreach (var fr in message.FunctionResponses)
            {
                var responseValue = BuildFunctionResponseValue(fr.ResponseJson);
                responseParts.Add(fr.Id is { Length: > 0 }
                    ? (object)new { functionResponse = new { name = fr.Name, id = fr.Id, response = responseValue } }
                    : new { functionResponse = new { name = fr.Name, response = responseValue } });
            }

            if (message.Attachments != null)
            {
                responseParts.AddRange(message.Attachments.Select(GetAttachmentPart));
            }

            return new { role = "user", parts = responseParts.ToArray() };
        }

        // Plain text (+ optional attachments) turn.
        var messageParts = new List<object> { new { text = message.Content } };
        if (message.Attachments != null)
        {
            messageParts.AddRange(message.Attachments.Select(GetAttachmentPart));
        }

        return new { role = message.Role == "assistant" ? "model" : "user", parts = messageParts.ToArray() };
    }

    /// <summary>
    /// Parses a raw tool-result string into a JSON object for the Gemini <c>functionResponse.response</c>
    /// field (which must be an object); non-object or invalid JSON is wrapped as <c>{ "result": ... }</c>.
    /// </summary>
    private static object BuildFunctionResponseValue(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new { };
        }

        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(responseJson);
            return node is System.Text.Json.Nodes.JsonObject ? node : new { result = node };
        }
        catch
        {
            return new { result = responseJson };
        }
    }

    private static Dictionary<string, object?> BuildGeminiPayload(GeminiRequest request)
    {
        var contentsParts = BuildContents(request);

        var hasTools = request.Tools != null && request.Tools.Count > 0;
        var useBuiltInSearch = request.EnableWebSearch;

        var payload = new Dictionary<string, object?>
        {
            ["systemInstruction"] = new { parts = new[] { new { text = request.SystemInstruction } } },
            ["contents"] = contentsParts.ToArray()
        };

        if (!string.IsNullOrWhiteSpace(request.ServiceTier))
        {
            payload["service_tier"] = request.ServiceTier;
        }

        var generationConfig = BuildGenerationConfig(request);
        if (generationConfig.Count > 0)
        {
            payload["generationConfig"] = generationConfig;
        }

        if (hasTools || useBuiltInSearch)
        {
            var toolsList = new List<object>();
            if (useBuiltInSearch)
            {
                toolsList.Add(new { googleSearch = new { } });
            }

            if (hasTools)
            {
                toolsList.AddRange(request.Tools!.Select(t => new
                {
                    functionDeclarations = t.FunctionDeclarations.Select(f => new
                    {
                        name = f.Name,
                        description = f.Description,
                        parameters = f.Parameters
                    })
                }));
            }

            payload["tools"] = toolsList;
            if (hasTools)
            {
                payload["toolConfig"] = new
                {
                    functionCallingConfig = new { mode = request.ToolConfig?.Mode ?? "AUTO" }
                };
            }
        }

        return payload;
    }

    private static string BuildGeminiPayloadJson(GeminiRequest request) =>
        JsonSerializer.Serialize(BuildGeminiPayload(request), JsonOptions);

    private async Task<GeminiResponse?> TryEnforcePromptTokenLimitAsync(
        GeminiRequest request,
        string modelName,
        CancellationToken cancellationToken)
    {
        if (request.MaxPromptTokens is not > 0)
        {
            return null;
        }

        var totalTokens = await CountPromptTokensAsync(request, modelName, cancellationToken);
        if (totalTokens <= request.MaxPromptTokens.Value)
        {
            return null;
        }

        _logger.LogWarning(
            "Gemini request prompt token count {TotalTokens} exceeded configured limit {MaxPromptTokens}",
            totalTokens,
            request.MaxPromptTokens.Value);
        UpdateSuccessRate();
        return GetFallbackResponse("GeminiInputTokenLimit");
    }

    private async Task<int> CountPromptTokensAsync(
        GeminiRequest request,
        string modelName,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["generateContentRequest"] = BuildGeminiPayload(request)
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var countRequest = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{modelName}:countTokens");
        countRequest.Headers.Add("x-goog-api-key", _apiKey);
        countRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(countRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini countTokens returned error: {StatusCode} - {Content}",
                response.StatusCode,
                responseContent);
            throw new InvalidOperationException("Gemini countTokens failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        return document.RootElement.TryGetProperty("totalTokens", out var totalTokens)
            ? totalTokens.GetInt32()
            : 0;
    }

    private static Dictionary<string, object?> BuildGenerationConfig(GeminiRequest request)
    {
        var generationConfig = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(request.ResponseMimeType))
        {
            generationConfig["responseMimeType"] = request.ResponseMimeType;
            if (request.ResponseSchema is not null)
            {
                generationConfig["responseSchema"] = request.ResponseSchema;
            }
        }

        if (request.MaxTokens is not null)
        {
            generationConfig["maxOutputTokens"] = request.MaxTokens.Value;
        }

        if (request.Temperature is not null)
        {
            generationConfig["temperature"] = request.Temperature.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.MediaResolution))
        {
            generationConfig["mediaResolution"] = request.MediaResolution;
        }

        if (request.ThinkingBudget is not null || request.IncludeThoughts)
        {
            var thinkingConfig = new Dictionary<string, object?>();
            if (request.ThinkingBudget is not null)
            {
                thinkingConfig["thinkingBudget"] = request.ThinkingBudget.Value;
            }

            if (request.IncludeThoughts)
            {
                thinkingConfig["includeThoughts"] = true;
            }

            generationConfig["thinkingConfig"] = thinkingConfig;
        }

        return generationConfig;
    }

    private GeminiResponse ParseGeminiResponse(JsonElement geminiResponse)
    {
        var candidates = geminiResponse.GetProperty("candidates");
        var firstCandidate = candidates.EnumerateArray().FirstOrDefault();

        if (firstCandidate.TryGetProperty("finishReason", out var finishReason) &&
            finishReason.GetString() == "SAFETY")
        {
            _logger.LogWarning("Gemini API blocked response due to safety filters");
            UpdateSuccessRate();
            return GetFallbackResponse("ValidationFailure");
        }

        var textParts = new List<string>();
        var thoughtParts = new List<string>();
        var functionCalls = new List<GeminiFunctionCall>();
        if (firstCandidate.TryGetProperty("content", out var contentProp) &&
            contentProp.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textProp))
                {
                    var isThought = part.TryGetProperty("thought", out var thoughtFlag) &&
                                    thoughtFlag.ValueKind == JsonValueKind.True &&
                                    thoughtFlag.GetBoolean();
                    if (isThought)
                        thoughtParts.Add(textProp.GetString() ?? string.Empty);
                    else
                        textParts.Add(textProp.GetString() ?? string.Empty);
                }
                else if (part.TryGetProperty("functionCall", out var fcProp))
                {
                    var fc = new GeminiFunctionCall
                    {
                        Name = fcProp.GetProperty("name").GetString() ?? string.Empty,
                        Id = fcProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : null,
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
                                // Arrays/objects: keep the structured value (cloned so it outlives
                                // the JsonDocument) so it re-serializes as a real JSON array/object
                                // when forwarded to a tool. GetRawText() would flatten it to a JSON
                                // string, which breaks array/object params downstream — e.g. the
                                // BFF rejects a stringified cad_commands as "At least one CAD command
                                // is required." It also keeps the multi-turn functionCall echo intact.
                                _ => arg.Value.Clone()
                            };
                        }
                    }

                    functionCalls.Add(fc);
                }
            }
        }

        var tokenUsage = geminiResponse.TryGetProperty("usageMetadata", out var usageMetadata)
            ? ParseTokenUsage(usageMetadata)
            : null;

        return new GeminiResponse
        {
            Success = true,
            Content = string.Join("", textParts),
            ThoughtContent = string.Join("", thoughtParts),
            FunctionCalls = functionCalls,
            TokenUsage = tokenUsage
        };
    }

    private static GeminiTokenUsage ParseTokenUsage(JsonElement usageMetadata) =>
        new()
        {
            PromptTokens = usageMetadata.TryGetProperty("promptTokenCount", out var promptTokens) ? promptTokens.GetInt32() : 0,
            CachedPromptTokens = usageMetadata.TryGetProperty("cachedContentTokenCount", out var cachedPromptTokens) ? cachedPromptTokens.GetInt32() : 0,
            ToolUsePromptTokens = usageMetadata.TryGetProperty("toolUsePromptTokenCount", out var toolUsePromptTokens) ? toolUsePromptTokens.GetInt32() : 0,
            ThoughtTokens = usageMetadata.TryGetProperty("thoughtsTokenCount", out var thoughtTokens) ? thoughtTokens.GetInt32() : 0,
            CompletionTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out var completionTokens) ? completionTokens.GetInt32() : 0,
            TotalTokens = usageMetadata.TryGetProperty("totalTokenCount", out var totalTokens) ? totalTokens.GetInt32() : 0
        };

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
            ["GeminiInputTokenLimit"] = "The uploaded content is too large for cost-effective AI processing. Please upload fewer pages, split the document into smaller files, or enter the key details manually.",
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
