using Maliev.ChatbotService.Application.Configuration;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Implementation of the Gemini API client using HttpClient.
/// </summary>
public class GeminiClient : IGeminiClient
{
    private const int MaxGroundingSources = 5;
    private const int MaxGroundingSourceTitleCharacters = 160;
    private const int MaxGroundingSourceUrlCharacters = 2048;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiClient> _logger;
    private readonly ConversationMetrics _metrics;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly IReadOnlyList<GeminiSafetySetting> _defaultSafetySettings;
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
        _defaultSafetySettings = GeminiSafetySettingsOptions.FromConfiguration(configuration).SafetySettings;
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
        var effectiveTimeoutSeconds = ResolveEffectiveTimeoutSeconds(request);

        try
        {
            var modelName = request.ModelName ?? _modelName;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeoutSeconds));

            var promptLimitResponse = await TryEnforcePromptTokenLimitAsync(request, modelName, cts.Token);
            if (promptLimitResponse is not null)
            {
                return promptLimitResponse;
            }

            var url = $"v1beta/models/{modelName}:generateContent";
            var json = BuildGeminiPayloadJson(request, modelName);
            var maxAttempts = ResolveMaxAttempts(request);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var messageRequest = new HttpRequestMessage(HttpMethod.Post, url);
                AddGeminiHeaders(messageRequest, request, effectiveTimeoutSeconds);
                messageRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(messageRequest, cts.Token);
                var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                var responseServiceTier = GetResponseServiceTier(response);

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
                        return WithServiceTier(GetFallbackResponse("GeminiAPIRateLimit"), responseServiceTier);
                    }

                    _logger.LogError(
                        "Gemini API returned error: {StatusCode} - {ErrorSummary}",
                        response.StatusCode,
                        ModelProviderErrorSanitizer.Summarize(response, responseContent));
                    UpdateSuccessRate();
                    return WithServiceTier(GetFallbackResponse("GeminiAPIError"), responseServiceTier);
                }

                using var document = JsonDocument.Parse(responseContent);
                var parsed = ParseGeminiResponse(document.RootElement);
                parsed.ServiceTier = responseServiceTier ?? parsed.ServiceTier;
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Gemini API request timed out after {Timeout} seconds", effectiveTimeoutSeconds);
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
        var groundingWebSearchQueries = new List<string>();
        var groundingSources = new List<GeminiGroundingSource>();
        var googleSearchGroundingPromptCount = 0;
        GeminiTokenUsage? tokenUsage = null;
        string? streamServiceTier = null;
        var effectiveTimeoutSeconds = ResolveEffectiveTimeoutSeconds(request);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeoutSeconds));

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
        var json = BuildGeminiPayloadJson(request, modelName);
        var maxAttempts = ResolveMaxAttempts(request);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var messageRequest = new HttpRequestMessage(HttpMethod.Post, url);
            AddGeminiHeaders(messageRequest, request, effectiveTimeoutSeconds);
            messageRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(
                messageRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);
            var responseServiceTier = GetResponseServiceTier(response);
            streamServiceTier = responseServiceTier;

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
                if (ShouldRetryFlexFailure(request, response.StatusCode, attempt, maxAttempts))
                {
                    var retryDelay = ResolveFlexRetryDelay(attempt);
                    _logger.LogWarning(
                        "Gemini Flex streaming request returned {StatusCode} on attempt {Attempt}/{MaxAttempts}. Retrying as Flex after {DelayMs}ms.",
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

                _logger.LogError(
                    "Gemini streaming API returned error: {StatusCode} - {ErrorSummary}",
                    response.StatusCode,
                    ModelProviderErrorSanitizer.Summarize(response, responseContent));
                UpdateSuccessRate();
                yield return new GeminiStreamEvent
                {
                    Type = "final",
                    Response = WithServiceTier(
                        GetFallbackResponse(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                            ? "GeminiAPIRateLimit"
                            : "GeminiAPIError"),
                        responseServiceTier)
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
                var parsed = ParseGeminiResponse(document.RootElement, allowCandidateLessResponse: true);
                parsed.ServiceTier = responseServiceTier ?? parsed.ServiceTier;
                streamServiceTier = parsed.ServiceTier ?? streamServiceTier;
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
                        Delta = parsed.Content,
                        Response = parsed
                    };
                }

                if (!string.IsNullOrEmpty(parsed.ThoughtContent))
                {
                    accumulatedThought.Append(parsed.ThoughtContent);
                    yield return new GeminiStreamEvent
                    {
                        Type = "thought",
                        Thought = parsed.ThoughtContent,
                        Response = parsed
                    };
                }

                if (parsed.FunctionCalls.Count > 0)
                {
                    functionCalls.AddRange(parsed.FunctionCalls);
                }

                if (parsed.GroundingWebSearchQueries.Count > 0)
                {
                    groundingWebSearchQueries.AddRange(parsed.GroundingWebSearchQueries);
                }

                if (parsed.GroundingSources.Count > 0)
                {
                    groundingSources.AddRange(parsed.GroundingSources);
                }

                if (parsed.GoogleSearchGroundingPromptCount > 0)
                {
                    googleSearchGroundingPromptCount = 1;
                }

                tokenUsage = parsed.TokenUsage ?? tokenUsage;
                if (string.IsNullOrEmpty(parsed.Content) &&
                    string.IsNullOrEmpty(parsed.ThoughtContent) &&
                    (parsed.TokenUsage is not null ||
                     parsed.GoogleSearchGroundingPromptCount > 0 ||
                     parsed.GroundingWebSearchQueries.Count > 0 ||
                     parsed.GroundingSources.Count > 0))
                {
                    yield return new GeminiStreamEvent
                    {
                        Type = "metadata",
                        Response = parsed
                    };
                }
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
                    TokenUsage = tokenUsage,
                    ServiceTier = streamServiceTier,
                    GroundingWebSearchQueries = groundingWebSearchQueries,
                    GroundingSources = NormalizeGroundingSources(groundingSources),
                    GoogleSearchGroundingPromptCount = googleSearchGroundingPromptCount
                }
            };
            yield break;
        }
    }

    private static string? GetResponseServiceTier(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-gemini-service-tier", out var values)
            ? values.FirstOrDefault()
            : null;

    private static GeminiResponse WithServiceTier(GeminiResponse response, string? serviceTier)
    {
        response.ServiceTier = serviceTier;
        return response;
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
        => MessagePipelinePolicy.IsSupportedGeminiExternalUrlMimeType(mimeType);

    private static int ResolveEffectiveTimeoutSeconds(GeminiRequest request)
    {
        var timeoutSeconds = request.TimeoutSeconds > 0
            ? request.TimeoutSeconds
            : 10;

        return IsFlexTier(request)
            ? Math.Max(timeoutSeconds, GeminiRequest.FlexInferenceTimeoutSeconds)
            : timeoutSeconds;
    }

    private static bool IsFlexTier(GeminiRequest request) =>
        string.Equals(request.ServiceTier, "flex", StringComparison.OrdinalIgnoreCase);

    private void AddGeminiHeaders(
        HttpRequestMessage messageRequest,
        GeminiRequest request,
        int effectiveTimeoutSeconds)
    {
        messageRequest.Headers.Add("x-goog-api-key", _apiKey);
        if (IsFlexTier(request))
        {
            messageRequest.Headers.Add("X-Server-Timeout", effectiveTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        }
    }

    private int ResolveMaxAttempts(GeminiRequest request) =>
        IsFlexTier(request)
            ? _flexRetryMaxAttempts
            : 1;

    private static bool ShouldRetryFlexFailure(
        GeminiRequest request,
        HttpStatusCode statusCode,
        int attempt,
        int maxAttempts)
    {
        if (!IsFlexTier(request) ||
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

        var topLevelAttachments = BuildTopLevelAttachments(request);

        // Legacy: merge top-level request media into the last plain-text user message.
        if (topLevelAttachments.Count > 0 && contentsParts.Count > 0)
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

                existingParts.AddRange(topLevelAttachments.Select(GetAttachmentPart));
                contentsParts[^1] = new { role = "user", parts = existingParts.ToArray() };
            }
        }

        return contentsParts;
    }

    private static List<GeminiAttachment> BuildTopLevelAttachments(GeminiRequest request)
    {
        var attachments = request.Attachments?.ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            attachments.Add(new GeminiAttachment
            {
                MimeType = ResolveImageUrlMimeType(request.ImageUrl),
                Data = request.ImageUrl.Trim()
            });
        }

        return attachments;
    }

    private static string ResolveImageUrlMimeType(string imageUrl)
    {
        var path = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : imageUrl;

        return path.Trim().ToLowerInvariant() switch
        {
            var value when value.EndsWith(".png", StringComparison.Ordinal) => "image/png",
            var value when value.EndsWith(".webp", StringComparison.Ordinal) => "image/webp",
            var value when value.EndsWith(".bmp", StringComparison.Ordinal) => "image/bmp",
            _ => "image/jpeg"
        };
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
                .Select(BuildFunctionCallPart)
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

    private static object BuildFunctionCallPart(GeminiFunctionCall functionCall)
    {
        var functionCallValue = functionCall.Id is { Length: > 0 }
            ? (object)new { name = functionCall.Name, args = functionCall.Args, id = functionCall.Id }
            : new { name = functionCall.Name, args = functionCall.Args };

        return functionCall.ThoughtSignature is { Length: > 0 }
            ? new { functionCall = functionCallValue, thoughtSignature = functionCall.ThoughtSignature }
            : new { functionCall = functionCallValue };
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

    internal static Dictionary<string, object?> BuildGeminiPayload(
        GeminiRequest request,
        IReadOnlyList<GeminiSafetySetting>? defaultSafetySettings = null,
        string? modelName = null)
    {
        var contentsParts = BuildContents(request);

        var hasTools = request.Tools != null && request.Tools.Count > 0;
        var useBuiltInSearch = request.EnableWebSearch;
        var useUrlContext = request.EnableUrlContext;

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = contentsParts.ToArray()
        };

        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            payload["systemInstruction"] = new { parts = new[] { new { text = request.SystemInstruction } } };
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceTier))
        {
            payload["serviceTier"] = request.ServiceTier;
        }

        if (!string.IsNullOrWhiteSpace(request.CachedContentName))
        {
            payload["cachedContent"] = request.CachedContentName;
        }

        if (request.Store is not null)
        {
            payload["store"] = request.Store.Value;
        }

        var safetySettings = ResolveSafetySettings(request, defaultSafetySettings);
        if (safetySettings.Count > 0)
        {
            payload["safetySettings"] = safetySettings.Select(setting => new
            {
                category = setting.Category,
                threshold = setting.Threshold
            }).ToArray();
        }

        var generationConfig = BuildGenerationConfig(request, request.ModelName ?? modelName);
        if (generationConfig.Count > 0)
        {
            payload["generationConfig"] = generationConfig;
        }

        if (hasTools || useBuiltInSearch || useUrlContext)
        {
            var toolsList = new List<object>();
            if (useBuiltInSearch)
            {
                toolsList.Add(new { google_search = new { } });
            }

            if (useUrlContext)
            {
                toolsList.Add(new { url_context = new { } });
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

    internal static Dictionary<string, object?> BuildGeminiCountTokensPayload(
        GeminiRequest request,
        IReadOnlyList<GeminiSafetySetting>? defaultSafetySettings,
        string modelName)
    {
        var generateContentRequest = BuildGeminiPayload(request, defaultSafetySettings, modelName);
        generateContentRequest["model"] = ToGeminiModelResourceName(modelName);

        return new Dictionary<string, object?>
        {
            ["generateContentRequest"] = generateContentRequest
        };
    }

    internal static string ToGeminiModelResourceName(string modelName) =>
        modelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelName
            : $"models/{modelName}";

    private string BuildGeminiPayloadJson(GeminiRequest request, string modelName) =>
        JsonSerializer.Serialize(BuildGeminiPayload(request, _defaultSafetySettings, modelName), JsonOptions);

    private static IReadOnlyList<GeminiSafetySetting> ResolveSafetySettings(
        GeminiRequest request,
        IReadOnlyList<GeminiSafetySetting>? defaultSafetySettings)
    {
        if (request.SafetySettings is { Count: > 0 })
        {
            return request.SafetySettings;
        }

        return defaultSafetySettings ?? [];
    }

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
        var json = JsonSerializer.Serialize(
            BuildGeminiCountTokensPayload(request, _defaultSafetySettings, modelName),
            JsonOptions);
        using var countRequest = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{modelName}:countTokens");
        countRequest.Headers.Add("x-goog-api-key", _apiKey);
        countRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(countRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini countTokens returned error: {StatusCode} - {ErrorSummary}",
                response.StatusCode,
                ModelProviderErrorSanitizer.Summarize(response, responseContent));
            throw new InvalidOperationException("Gemini countTokens failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        return document.RootElement.TryGetProperty("totalTokens", out var totalTokens)
            ? totalTokens.GetInt32()
            : 0;
    }

    private static Dictionary<string, object?> BuildGenerationConfig(GeminiRequest request, string? modelName)
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

        var thinkingBudget = request.ThinkingBudget ??
            (ShouldDefaultDisableThinking(modelName) ? 0 : null);
        if (thinkingBudget is not null || request.IncludeThoughts)
        {
            var thinkingConfig = new Dictionary<string, object?>();
            if (thinkingBudget is not null)
            {
                thinkingConfig["thinkingBudget"] = thinkingBudget.Value;
            }

            if (request.IncludeThoughts)
            {
                thinkingConfig["includeThoughts"] = true;
            }

            generationConfig["thinkingConfig"] = thinkingConfig;
        }

        return generationConfig;
    }

    private static bool ShouldDefaultDisableThinking(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        var normalizedModelName = modelName.Trim();
        if (normalizedModelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedModelName = normalizedModelName["models/".Length..];
        }

        return normalizedModelName.StartsWith("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase);
    }

    private GeminiResponse ParseGeminiResponse(JsonElement geminiResponse, bool allowCandidateLessResponse = false)
    {
        var tokenUsage = geminiResponse.TryGetProperty("usageMetadata", out var usageMetadata) &&
            usageMetadata.ValueKind == JsonValueKind.Object
                ? ParseTokenUsage(usageMetadata)
                : null;
        var serviceTier = usageMetadata.ValueKind == JsonValueKind.Object &&
            usageMetadata.TryGetProperty("serviceTier", out var serviceTierElement)
                ? serviceTierElement.GetString()
                : null;

        var promptFeedbackFallback = TryBuildPromptFeedbackFallback(geminiResponse, tokenUsage, serviceTier);
        if (promptFeedbackFallback is not null)
        {
            return promptFeedbackFallback;
        }

        if (!geminiResponse.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            if (allowCandidateLessResponse)
            {
                return new GeminiResponse
                {
                    Success = true,
                    TokenUsage = tokenUsage,
                    ServiceTier = serviceTier
                };
            }

            throw new JsonException("Gemini response did not include candidates.");
        }

        var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
        if (firstCandidate.ValueKind != JsonValueKind.Object)
        {
            if (allowCandidateLessResponse)
            {
                return new GeminiResponse
                {
                    Success = true,
                    TokenUsage = tokenUsage,
                    ServiceTier = serviceTier
                };
            }

            throw new JsonException("Gemini response did not include a candidate object.");
        }

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
                        ThoughtSignature = part.TryGetProperty("thoughtSignature", out var thoughtSignatureProp)
                            ? thoughtSignatureProp.GetString()
                            : null,
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

        var groundingWebSearchQueries = ParseGroundingWebSearchQueries(firstCandidate);
        var groundingSources = ParseGroundingSources(firstCandidate);

        return new GeminiResponse
        {
            Success = true,
            Content = string.Join("", textParts),
            ThoughtContent = string.Join("", thoughtParts),
            FunctionCalls = functionCalls,
            TokenUsage = tokenUsage,
            ServiceTier = serviceTier,
            GroundingWebSearchQueries = groundingWebSearchQueries,
            GroundingSources = groundingSources,
            GoogleSearchGroundingPromptCount = groundingWebSearchQueries.Count > 0 || groundingSources.Count > 0 ? 1 : 0
        };
    }

    private static List<GeminiGroundingSource> ParseGroundingSources(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("groundingMetadata", out var groundingMetadata) ||
            groundingMetadata.ValueKind != JsonValueKind.Object ||
            !groundingMetadata.TryGetProperty("groundingChunks", out var groundingChunks) ||
            groundingChunks.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var sources = new List<GeminiGroundingSource>();
        foreach (var chunk in groundingChunks.EnumerateArray())
        {
            if (chunk.ValueKind != JsonValueKind.Object ||
                !chunk.TryGetProperty("web", out var web) ||
                web.ValueKind != JsonValueKind.Object ||
                !web.TryGetProperty("uri", out var uriElement))
            {
                continue;
            }

            var rawUrl = uriElement.GetString();
            if (!TryNormalizeHttpsUrl(rawUrl, out var normalizedUrl))
            {
                continue;
            }

            var rawTitle = web.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString()
                : null;
            var title = SanitizeGroundingSourceTitle(rawTitle, new Uri(normalizedUrl).Host);
            sources.Add(new GeminiGroundingSource
            {
                Title = title,
                Url = normalizedUrl,
                Domain = new Uri(normalizedUrl).Host
            });
        }

        return NormalizeGroundingSources(sources);
    }

    private static List<GeminiGroundingSource> NormalizeGroundingSources(
        IEnumerable<GeminiGroundingSource> sources)
    {
        return sources
            .Where(source => TryNormalizeHttpsUrl(source.Url, out _))
            .Select(source =>
            {
                TryNormalizeHttpsUrl(source.Url, out var normalizedUrl);
                var title = SanitizeGroundingSourceTitle(source.Title, new Uri(normalizedUrl).Host);
                return new GeminiGroundingSource
                {
                    Title = title,
                    Url = normalizedUrl,
                    Domain = new Uri(normalizedUrl).Host
                };
            })
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(source => source.Title, StringComparer.Ordinal)
                .First())
            .OrderBy(source => source.Url, StringComparer.Ordinal)
            .ThenBy(source => source.Title, StringComparer.Ordinal)
            .Take(MaxGroundingSources)
            .ToList();
    }

    private static bool TryNormalizeHttpsUrl(string? value, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxGroundingSourceUrlCharacters ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };
        normalizedUrl = builder.Uri.AbsoluteUri.TrimEnd('/');
        return normalizedUrl.Length <= MaxGroundingSourceUrlCharacters;
    }

    private static string SanitizeGroundingSourceTitle(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var bounded = value[..Math.Min(value.Length, 1024)];
        var decoded = WebUtility.HtmlDecode(bounded);
        var withoutTags = Regex.Replace(
            decoded,
            "<[^>]*>",
            " ",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));
        var normalized = string.Join(
            ' ',
            withoutTags.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return normalized[..Math.Min(normalized.Length, MaxGroundingSourceTitleCharacters)];
    }

    private static List<string> ParseGroundingWebSearchQueries(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("groundingMetadata", out var groundingMetadata) ||
            groundingMetadata.ValueKind != JsonValueKind.Object ||
            !groundingMetadata.TryGetProperty("webSearchQueries", out var webSearchQueries) ||
            webSearchQueries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var queries = new List<string>();
        foreach (var query in webSearchQueries.EnumerateArray())
        {
            var value = query.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                queries.Add(value);
            }
        }

        return queries;
    }

    private GeminiResponse? TryBuildPromptFeedbackFallback(
        JsonElement geminiResponse,
        GeminiTokenUsage? tokenUsage,
        string? serviceTier)
    {
        if (!geminiResponse.TryGetProperty("promptFeedback", out var promptFeedback) ||
            promptFeedback.ValueKind != JsonValueKind.Object ||
            !promptFeedback.TryGetProperty("blockReason", out var blockReasonElement))
        {
            return null;
        }

        var blockReason = blockReasonElement.GetString();
        if (string.IsNullOrWhiteSpace(blockReason) ||
            blockReason.Equals("BLOCK_REASON_UNSPECIFIED", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        _logger.LogWarning("Gemini API blocked prompt due to {BlockReason}", blockReason);
        var fallback = GetFallbackResponse("ValidationFailure");
        fallback.TokenUsage = tokenUsage;
        fallback.ServiceTier = serviceTier;
        return fallback;
    }

    private static GeminiTokenUsage ParseTokenUsage(JsonElement usageMetadata) =>
        new()
        {
            PromptTokens = usageMetadata.TryGetProperty("promptTokenCount", out var promptTokens) ? promptTokens.GetInt32() : 0,
            CachedPromptTokens = usageMetadata.TryGetProperty("cachedContentTokenCount", out var cachedPromptTokens) ? cachedPromptTokens.GetInt32() : 0,
            ToolUsePromptTokens = usageMetadata.TryGetProperty("toolUsePromptTokenCount", out var toolUsePromptTokens) ? toolUsePromptTokens.GetInt32() : 0,
            ThoughtTokens = usageMetadata.TryGetProperty("thoughtsTokenCount", out var thoughtTokens) ? thoughtTokens.GetInt32() : 0,
            CompletionTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out var completionTokens) ? completionTokens.GetInt32() : 0,
            TotalTokens = usageMetadata.TryGetProperty("totalTokenCount", out var totalTokens) ? totalTokens.GetInt32() : 0,
            PromptTokenDetails = ParseModalityTokenDetails(usageMetadata, "promptTokensDetails"),
            CachedTokenDetails = ParseModalityTokenDetails(usageMetadata, "cacheTokensDetails"),
            CandidateTokenDetails = ParseModalityTokenDetails(usageMetadata, "candidatesTokensDetails"),
            ToolUsePromptTokenDetails = ParseModalityTokenDetails(usageMetadata, "toolUsePromptTokensDetails")
        };

    private static List<GeminiModalityTokenCount> ParseModalityTokenDetails(
        JsonElement usageMetadata,
        string propertyName)
    {
        if (!usageMetadata.TryGetProperty(propertyName, out var details) ||
            details.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var tokenCounts = new List<GeminiModalityTokenCount>();
        foreach (var detail in details.EnumerateArray())
        {
            tokenCounts.Add(new GeminiModalityTokenCount
            {
                Modality = detail.TryGetProperty("modality", out var modality)
                    ? modality.GetString() ?? string.Empty
                    : string.Empty,
                TokenCount = detail.TryGetProperty("tokenCount", out var tokenCount)
                    ? tokenCount.GetInt32()
                    : 0
            });
        }

        return tokenCounts;
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
