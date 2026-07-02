using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Model provider adapter for OpenAI-compatible chat completion APIs.
/// </summary>
public sealed class OpenAICompatibleModelProviderClient : IModelProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAICompatibleModelProviderClient> _logger;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly string _chatCompletionsPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAICompatibleModelProviderClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public OpenAICompatibleModelProviderClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAICompatibleModelProviderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Llm:OpenAICompatible:ApiKey"]
            ?? configuration["OpenAICompatible:ApiKey"]
            ?? string.Empty;
        _modelName = configuration["Llm:OpenAICompatible:ModelName"]
            ?? configuration["OpenAICompatible:ModelName"]
            ?? "openai-compatible-model";
        _chatCompletionsPath = ResolveChatCompletionsPath(
            configuration["Llm:OpenAICompatible:ChatCompletionsPath"] ??
            configuration["OpenAICompatible:ChatCompletionsPath"],
            httpClient.BaseAddress);
    }

    /// <inheritdoc/>
    public string ProviderName => "openai-compatible";

    /// <inheritdoc/>
    public async Task<GeminiResponse> SendMessageAsync(
        GeminiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI-compatible API key is not configured. Set 'Llm:OpenAICompatible:ApiKey'.");
        }

        if (TryGetUnsupportedAttachmentMimeType(request, out var unsupportedMimeType))
        {
            _logger.LogWarning(
                "OpenAI-compatible provider request contains unsupported attachment MIME type {MimeType}",
                unsupportedMimeType);
            return GetFallbackResponse("ModelProviderUnsupportedAttachment");
        }

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

            var payload = BuildPayload(request, modelName, stream: false);
            var json = JsonSerializer.Serialize(payload, JsonOptions);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _chatCompletionsPath);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "OpenAI-compatible provider returned error: {StatusCode} - {Content}",
                    response.StatusCode,
                    responseContent);
                return GetFallbackResponse(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? "ModelProviderRateLimit"
                    : "ModelProviderError");
            }

            using var document = JsonDocument.Parse(responseContent);
            var parsed = ParseResponse(document.RootElement);
            parsed.ServiceTier = GetResponseServiceTier(response) ?? parsed.ServiceTier;
            return parsed;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OpenAI-compatible provider request timed out after {Timeout} seconds", request.TimeoutSeconds);
            return GetFallbackResponse("ModelProviderTimeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI-compatible provider");
            return GetFallbackResponse("UnexpectedError");
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(
        GeminiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new GeminiStreamEvent { Type = "started" };

        if (request.Tools is { Count: > 0 })
        {
            var toolResponse = await SendMessageAsync(request, cancellationToken);
            if (!string.IsNullOrEmpty(toolResponse.Content))
            {
                yield return new GeminiStreamEvent { Type = "delta", Delta = toolResponse.Content };
            }

            yield return new GeminiStreamEvent { Type = "final", Response = toolResponse };
            yield break;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            yield return new GeminiStreamEvent
            {
                Type = "final",
                Response = GetFallbackResponse("ModelProviderMissingApiKey")
            };
            yield break;
        }

        if (TryGetUnsupportedAttachmentMimeType(request, out var unsupportedMimeType))
        {
            _logger.LogWarning(
                "OpenAI-compatible provider streaming request contains unsupported attachment MIME type {MimeType}",
                unsupportedMimeType);
            yield return new GeminiStreamEvent
            {
                Type = "final",
                Response = GetFallbackResponse("ModelProviderUnsupportedAttachment")
            };
            yield break;
        }

        var accumulatedText = new StringBuilder();
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

        var payload = BuildPayload(request, modelName, stream: true);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _chatCompletionsPath);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogError(
                "OpenAI-compatible streaming provider returned error: {StatusCode} - {Content}",
                response.StatusCode,
                responseContent);
            yield return new GeminiStreamEvent
            {
                Type = "final",
                Response = GetFallbackResponse(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? "ModelProviderRateLimit"
                    : "ModelProviderError")
            };
            yield break;
        }

        var streamServiceTier = GetResponseServiceTier(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            if (data.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            using var document = JsonDocument.Parse(data);
            streamServiceTier = ParseServiceTier(document.RootElement) ?? streamServiceTier;
            if (document.RootElement.TryGetProperty("usage", out var usage) &&
                usage.ValueKind == JsonValueKind.Object)
            {
                tokenUsage = ParseTokenUsage(usage);
            }

            var delta = ParseStreamDelta(document.RootElement);
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            accumulatedText.Append(delta);
            yield return new GeminiStreamEvent { Type = "delta", Delta = delta };
        }

        yield return new GeminiStreamEvent
        {
            Type = "final",
            Response = new GeminiResponse
            {
                Success = true,
                Content = accumulatedText.ToString(),
                TokenUsage = tokenUsage,
                ServiceTier = streamServiceTier
            }
        };
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static JsonSerializerOptions GeminiJsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string? GetResponseServiceTier(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-gemini-service-tier", out var values)
            ? values.FirstOrDefault()
            : null;

    private async Task<GeminiResponse?> TryEnforcePromptTokenLimitAsync(
        GeminiRequest request,
        string modelName,
        CancellationToken cancellationToken)
    {
        if (request.MaxPromptTokens is not > 0 ||
            !SupportsGeminiNativeTokenCounting(modelName))
        {
            return null;
        }

        int totalTokens;
        try
        {
            totalTokens = await CountPromptTokensAsync(request, modelName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Gemini OpenAI-compatible prompt token preflight failed for model {ModelName}; skipping generation.",
                modelName);
            return GetFallbackResponse("ModelProviderError");
        }

        if (totalTokens <= request.MaxPromptTokens.Value)
        {
            return null;
        }

        _logger.LogWarning(
            "Gemini OpenAI-compatible request prompt token count {TotalTokens} exceeded configured limit {MaxPromptTokens}",
            totalTokens,
            request.MaxPromptTokens.Value);
        return GetFallbackResponse("GeminiInputTokenLimit");
    }

    private async Task<int> CountPromptTokensAsync(
        GeminiRequest request,
        string modelName,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["generateContentRequest"] = GeminiClient.BuildGeminiPayload(request, defaultSafetySettings: null, modelName)
        };

        using var countRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1beta/models/{NormalizeGeminiModelName(modelName)}:countTokens");
        countRequest.Headers.Add("x-goog-api-key", _apiKey);
        countRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload, GeminiJsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(countRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini OpenAI-compatible countTokens returned error: {StatusCode} - {Content}",
                response.StatusCode,
                responseContent);
            throw new InvalidOperationException("Gemini countTokens failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        return document.RootElement.TryGetProperty("totalTokens", out var totalTokens)
            ? totalTokens.GetInt32()
            : 0;
    }

    private bool SupportsGeminiNativeTokenCounting(string modelName) =>
        IsGeminiModel(modelName) &&
        _httpClient.BaseAddress?.Host.Equals(
            "generativelanguage.googleapis.com",
            StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeGeminiModelName(string modelName)
    {
        var normalizedModelName = modelName.Trim();
        return normalizedModelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? normalizedModelName["models/".Length..]
            : normalizedModelName;
    }

    private static object BuildPayload(GeminiRequest request, string modelName, bool stream)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            messages.Add(new
            {
                role = "system",
                content = request.SystemInstruction
            });
        }

        var requestMessages = request.Messages.Count > 0
            ? request.Messages
            : BuildPromptMessages(request.Prompt);

        foreach (var message in requestMessages)
        {
            // Model turn that issued tool calls -> assistant message with tool_calls.
            if (message.FunctionCalls is { Count: > 0 })
            {
                messages.Add(new
                {
                    role = "assistant",
                    content = (string?)null,
                    tool_calls = message.FunctionCalls.Select(fc => new
                    {
                        id = ToolCallId(fc.Id, fc.Name),
                        type = "function",
                        function = new
                        {
                            name = fc.Name,
                            arguments = JsonSerializer.Serialize(fc.Args, JsonOptions)
                        }
                    }).ToArray()
                });
                continue;
            }

            // Turn that returns tool results -> one "tool" role message per response.
            if (message.FunctionResponses is { Count: > 0 })
            {
                foreach (var fr in message.FunctionResponses)
                {
                    messages.Add(new
                    {
                        role = "tool",
                        tool_call_id = ToolCallId(fr.Id, fr.Name),
                        content = fr.ResponseJson
                    });
                }

                continue;
            }

            messages.Add(new
            {
                role = message.Role == "assistant" ? "assistant" : "user",
                content = BuildMessageContent(message)
            });
        }

        if (request.Attachments is { Count: > 0 } && messages.Count > 0)
        {
            var lastMessage = requestMessages.LastOrDefault();
            if (lastMessage is not null &&
                lastMessage.Role != "assistant" &&
                lastMessage.FunctionCalls is null &&
                lastMessage.FunctionResponses is null)
            {
                messages[^1] = new
                {
                    role = "user",
                    content = BuildMessageContent(new GeminiMessage
                    {
                        Role = lastMessage.Role,
                        Content = lastMessage.Content,
                        Attachments = MergeAttachments(lastMessage.Attachments, request.Attachments)
                    })
                };
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = modelName,
            ["messages"] = messages,
            ["stream"] = stream
        };

        if (stream)
        {
            payload["stream_options"] = new { include_usage = true };
        }

        if (request.MaxTokens is not null)
        {
            payload["max_tokens"] = request.MaxTokens.Value;
        }

        if (request.Temperature is not null)
        {
            payload["temperature"] = request.Temperature.Value;
        }

        if (request.Store is not null)
        {
            payload["store"] = request.Store.Value;
        }

        var reasoningEffort = ResolveReasoningEffort(request, modelName);
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            payload["reasoning_effort"] = reasoningEffort;
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceTier))
        {
            payload["service_tier"] = request.ServiceTier;
        }

        var googleExtraBody = BuildGoogleExtraBody(request, modelName);
        if (googleExtraBody.Count > 0)
        {
            payload["extra_body"] = new Dictionary<string, object?>
            {
                ["google"] = googleExtraBody
            };
        }

        if (!string.IsNullOrWhiteSpace(request.ResponseMimeType))
        {
            payload["response_format"] = request.ResponseSchema is null
                ? new { type = "json_object" }
                : new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "response",
                        schema = request.ResponseSchema
                    }
                };
        }

        if (request.Tools is { Count: > 0 })
        {
            payload["tools"] = request.Tools
                .SelectMany(tool => tool.FunctionDeclarations)
                .Select(function => new
                {
                    type = "function",
                    function = new
                    {
                        name = function.Name,
                        description = function.Description,
                        parameters = function.Parameters
                    }
                })
                .ToArray();

            payload["tool_choice"] = request.ToolConfig?.Mode?.ToUpperInvariant() switch
            {
                "ANY" => "required",
                "NONE" => "none",
                _ => "auto"
            };
        }

        return payload;
    }

    private static Dictionary<string, object?> BuildGoogleExtraBody(GeminiRequest request, string modelName)
    {
        var google = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(request.CachedContentName))
        {
            google["cached_content"] = request.CachedContentName;
        }

        var thinkingConfig = BuildGoogleThinkingConfig(request, modelName);
        if (thinkingConfig.Count > 0)
        {
            google["thinking_config"] = thinkingConfig;
        }

        return google;
    }

    private static Dictionary<string, object?> BuildGoogleThinkingConfig(GeminiRequest request, string modelName)
    {
        var thinkingConfig = new Dictionary<string, object?>();
        var thinkingBudget = request.ThinkingBudget;

        if (thinkingBudget == 0 &&
            !request.IncludeThoughts &&
            ShouldUseReasoningEffortNone(modelName))
        {
            thinkingBudget = null;
        }

        if (thinkingBudget == 0 &&
            !request.IncludeThoughts &&
            !IsGeminiModel(modelName))
        {
            thinkingBudget = null;
        }

        if (thinkingBudget is not null)
        {
            thinkingConfig["thinking_budget"] = thinkingBudget.Value;
        }

        if (request.IncludeThoughts)
        {
            thinkingConfig["include_thoughts"] = true;
        }

        return thinkingConfig;
    }

    private static string? ResolveReasoningEffort(GeminiRequest request, string modelName)
    {
        if (!ShouldUseReasoningEffortNone(modelName))
        {
            return null;
        }

        if (request.IncludeThoughts)
        {
            return null;
        }

        if (request.ThinkingBudget is null ||
            request.ThinkingBudget == 0)
        {
            return "none";
        }

        return null;
    }

    private static bool ShouldUseReasoningEffortNone(string? modelName) =>
        IsGemini25FlashModel(modelName);

    private static bool IsGeminiModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        var normalizedModelName = NormalizeGeminiModelName(modelName);
        return normalizedModelName.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGemini25FlashModel(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return false;
        }

        var normalizedModelName = NormalizeGeminiModelName(modelName);
        return normalizedModelName.StartsWith("gemini-2.5-flash", StringComparison.OrdinalIgnoreCase);
    }

    private static List<GeminiMessage> BuildPromptMessages(string? prompt) =>
        string.IsNullOrWhiteSpace(prompt)
            ? new List<GeminiMessage>()
            : new List<GeminiMessage> { new() { Role = "user", Content = prompt } };

    private static string ResolveDefaultChatCompletionsPath(Uri? baseAddress)
    {
        if (baseAddress is null)
        {
            return "v1/chat/completions";
        }

        var basePath = baseAddress.AbsolutePath.TrimEnd('/');
        if (baseAddress.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            return basePath.EndsWith("/openai", StringComparison.OrdinalIgnoreCase)
                ? "chat/completions"
                : "v1beta/openai/chat/completions";
        }

        return basePath.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "chat/completions"
            : "v1/chat/completions";
    }

    private static string ResolveChatCompletionsPath(string? configuredPath, Uri? baseAddress) =>
        string.IsNullOrWhiteSpace(configuredPath)
            ? ResolveDefaultChatCompletionsPath(baseAddress)
            : configuredPath.Trim().TrimStart('/');

    private static object BuildMessageContent(GeminiMessage message)
    {
        if (message.Attachments is not { Count: > 0 })
        {
            return message.Content;
        }

        var parts = new List<object>
        {
            new { type = "text", text = message.Content }
        };

        foreach (var attachment in message.Attachments)
        {
            if (attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new
                {
                    type = "image_url",
                    image_url = new { url = NormalizeImageUrl(attachment) }
                });
                continue;
            }

            if (TryBuildInputAudioPart(attachment, out var audioPart))
            {
                parts.Add(audioPart);
                continue;
            }

            parts.Add(new
            {
                type = "text",
                text = $"Attached file available as supplemental context: {attachment.MimeType}."
            });
        }

        return parts;
    }

    private static List<GeminiAttachment> MergeAttachments(
        List<GeminiAttachment>? messageAttachments,
        List<GeminiAttachment> requestAttachments)
    {
        var merged = new List<GeminiAttachment>();
        if (messageAttachments is not null)
        {
            merged.AddRange(messageAttachments);
        }

        merged.AddRange(requestAttachments);
        return merged;
    }

    private static string NormalizeImageUrl(GeminiAttachment attachment)
    {
        if (attachment.Data.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
            attachment.Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return attachment.Data;
        }

        return $"data:{attachment.MimeType};base64,{attachment.Data}";
    }

    private static bool TryGetUnsupportedAttachmentMimeType(GeminiRequest request, out string unsupportedMimeType)
    {
        foreach (var attachment in EnumerateAttachments(request))
        {
            if (!IsSerializableAttachment(attachment))
            {
                unsupportedMimeType = string.IsNullOrWhiteSpace(attachment.MimeType)
                    ? "(missing)"
                    : attachment.MimeType;
                return true;
            }
        }

        unsupportedMimeType = string.Empty;
        return false;
    }

    private static IEnumerable<GeminiAttachment> EnumerateAttachments(GeminiRequest request)
    {
        if (request.Attachments is not null)
        {
            foreach (var attachment in request.Attachments)
            {
                yield return attachment;
            }
        }

        var messages = request.Messages.Count > 0
            ? request.Messages
            : BuildPromptMessages(request.Prompt);
        foreach (var attachment in messages.SelectMany(message => message.Attachments ?? []))
        {
            yield return attachment;
        }
    }

    private static bool IsSerializableAttachment(GeminiAttachment attachment) =>
        attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
        TryBuildInputAudioPart(attachment, out _);

    private static bool TryBuildInputAudioPart(GeminiAttachment attachment, out object audioPart)
    {
        audioPart = new { };

        if (!attachment.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            !TryResolveAudioFormat(attachment.MimeType, out var format) ||
            !TryExtractInlineBase64(attachment.Data, out var data))
        {
            return false;
        }

        audioPart = new
        {
            type = "input_audio",
            input_audio = new
            {
                data,
                format
            }
        };
        return true;
    }

    private static bool TryResolveAudioFormat(string mimeType, out string format)
    {
        format = string.Empty;
        var normalized = mimeType.Trim().ToLowerInvariant();
        format = normalized switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => "wav",
            "audio/mpeg" or "audio/mp3" => "mp3",
            _ => string.Empty
        };

        return format.Length > 0;
    }

    private static bool TryExtractInlineBase64(string data, out string base64Data)
    {
        base64Data = string.Empty;
        if (data.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            data.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            data.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        base64Data = data.Contains(',')
            ? data.Split(',', 2)[1]
            : data;
        return !string.IsNullOrWhiteSpace(base64Data);
    }

    // OpenAI requires a tool message's tool_call_id to match the assistant tool_call id. Real
    // responses always carry an id; the name-based fallback only guards the degenerate id-less case.
    private static string ToolCallId(string? id, string name) =>
        string.IsNullOrWhiteSpace(id) ? $"call_{name}" : id;

    private static GeminiResponse ParseResponse(JsonElement root)
    {
        var choice = root.GetProperty("choices").EnumerateArray().FirstOrDefault();
        var message = choice.GetProperty("message");
        var content = message.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString() ?? string.Empty
            : string.Empty;
        var functionCalls = new List<GeminiFunctionCall>();

        if (message.TryGetProperty("tool_calls", out var toolCalls))
        {
            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                if (!toolCall.TryGetProperty("function", out var function))
                {
                    continue;
                }

                functionCalls.Add(new GeminiFunctionCall
                {
                    Name = function.TryGetProperty("name", out var name)
                        ? name.GetString() ?? string.Empty
                        : string.Empty,
                    Id = toolCall.TryGetProperty("id", out var idElement)
                        ? idElement.GetString()
                        : null,
                    Args = ParseArguments(function.TryGetProperty("arguments", out var arguments)
                        ? arguments.GetString()
                        : null)
                });
            }
        }

        var tokenUsage = root.TryGetProperty("usage", out var usage) &&
            usage.ValueKind == JsonValueKind.Object
                ? ParseTokenUsage(usage)
                : null;

        return new GeminiResponse
        {
            Success = true,
            Content = content,
            FunctionCalls = functionCalls,
            TokenUsage = tokenUsage,
            ServiceTier = ParseServiceTier(root)
        };
    }

    private static Dictionary<string, object> ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return new Dictionary<string, object>();
        }

        using var document = JsonDocument.Parse(arguments);
        return document.RootElement.EnumerateObject()
            .ToDictionary(
                item => item.Name,
                item => item.Value.ValueKind switch
                {
                    JsonValueKind.String => (object)(item.Value.GetString() ?? string.Empty),
                    JsonValueKind.Number => item.Value.TryGetInt64(out var longValue) ? longValue : item.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => item.Value.GetRawText()
                });
    }

    private static string ParseStreamDelta(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var choice = choices.EnumerateArray().FirstOrDefault();
        if (choice.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (!choice.TryGetProperty("delta", out var delta) ||
            !delta.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return content.GetString() ?? string.Empty;
    }

    private static string? ParseServiceTier(JsonElement root) =>
        root.TryGetProperty("service_tier", out var serviceTier) &&
        serviceTier.ValueKind == JsonValueKind.String
            ? serviceTier.GetString()
            : null;

    private static GeminiTokenUsage ParseTokenUsage(JsonElement usage)
    {
        var promptTokens = GetInt32(usage, "prompt_tokens");
        var completionTokens = GetInt32(usage, "completion_tokens");
        var totalTokens = GetInt32(usage, "total_tokens");
        var cachedPromptTokens = 0;
        var audioPromptTokens = 0;
        var reasoningTokens = 0;

        if (usage.TryGetProperty("prompt_tokens_details", out var promptDetails) &&
            promptDetails.ValueKind == JsonValueKind.Object)
        {
            cachedPromptTokens = GetInt32(promptDetails, "cached_tokens");
            audioPromptTokens = GetInt32(promptDetails, "audio_tokens");
        }

        if (usage.TryGetProperty("completion_tokens_details", out var completionDetails) &&
            completionDetails.ValueKind == JsonValueKind.Object)
        {
            reasoningTokens = GetInt32(completionDetails, "reasoning_tokens");
        }

        return new GeminiTokenUsage
        {
            PromptTokens = promptTokens,
            CompletionTokens = Math.Max(0, completionTokens - reasoningTokens),
            CachedPromptTokens = cachedPromptTokens,
            ThoughtTokens = reasoningTokens,
            TotalTokens = totalTokens,
            PromptTokenDetails = BuildOpenAiPromptTokenDetails(promptTokens, audioPromptTokens)
        };
    }

    private static List<GeminiModalityTokenCount> BuildOpenAiPromptTokenDetails(
        int promptTokens,
        int audioPromptTokens)
    {
        if (promptTokens <= 0)
        {
            return [];
        }

        var boundedAudioTokens = Math.Clamp(audioPromptTokens, 0, promptTokens);
        var textTokens = promptTokens - boundedAudioTokens;
        var details = new List<GeminiModalityTokenCount>();
        if (textTokens > 0)
        {
            details.Add(new GeminiModalityTokenCount { Modality = "TEXT", TokenCount = textTokens });
        }

        if (boundedAudioTokens > 0)
        {
            details.Add(new GeminiModalityTokenCount { Modality = "AUDIO", TokenCount = boundedAudioTokens });
        }

        return details;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        return property.TryGetInt32(out var value) ? value : 0;
    }

    private static GeminiResponse GetFallbackResponse(string errorType)
    {
        var message = errorType switch
        {
            "ModelProviderMissingApiKey" => "AI provider configuration is missing. Please contact support at info@maliev.com.",
            "ModelProviderRateLimit" => "We have exceeded the AI processing limit for now. Please try again in a few minutes.",
            "ModelProviderTimeout" => "I apologize, but I'm experiencing delays in processing your request. Please try again in a few moments.",
            "ModelProviderError" => "I apologize, but I'm temporarily unable to process your request. Please try again in a few moments.",
            "ModelProviderUnsupportedAttachment" => "This AI provider cannot process one or more uploaded files. Please retry without that attachment or use the Gemini provider.",
            "GeminiInputTokenLimit" => "The uploaded content is too large for cost-effective AI processing. Please upload fewer pages, split the document into smaller files, or enter the key details manually.",
            _ => "I apologize for the inconvenience. Something unexpected occurred. Please try again."
        };

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
