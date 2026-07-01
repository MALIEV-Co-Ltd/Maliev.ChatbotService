using System.Globalization;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Native Gemini Batch API client for non-urgent generateContent jobs.
/// </summary>
public sealed class GeminiBatchClient : IModelBatchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiBatchClient> _logger;
    private readonly string _apiKey;
    private readonly string _modelName;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiBatchClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">The logger.</param>
    public GeminiBatchClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiBatchClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured. Set 'Gemini:ApiKey'.");
        _modelName = configuration["Gemini:MainModelName"] ?? "gemini-2.5-flash";
    }

    /// <inheritdoc/>
    public async Task<ModelBatchJob> CreateInlineGenerateContentBatchAsync(
        ModelBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Requests.Count == 0)
        {
            throw new ArgumentException("At least one inline batch request is required.", nameof(request));
        }

        var modelName = NormalizeModelName(request.ModelName ?? _modelName);
        var payload = BuildCreateBatchPayload(request);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{modelName}:batchGenerateContent");
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini Batch API create returned {StatusCode}: {Content}",
                response.StatusCode,
                responseContent);
            throw new InvalidOperationException("Gemini Batch API create failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        return ParseBatchJob(document.RootElement);
    }

    /// <inheritdoc/>
    public async Task<ModelBatchJob> GetBatchAsync(
        string batchName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(batchName))
        {
            throw new ArgumentException("Batch name is required.", nameof(batchName));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"v1beta/{batchName}");
        httpRequest.Headers.Add("x-goog-api-key", _apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini Batch API get returned {StatusCode}: {Content}",
                response.StatusCode,
                responseContent);
            throw new InvalidOperationException("Gemini Batch API get failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        return ParseBatchJob(document.RootElement);
    }

    private static Dictionary<string, object?> BuildCreateBatchPayload(ModelBatchRequest request)
    {
        var batch = new Dictionary<string, object?>
        {
            ["display_name"] = request.DisplayName,
            ["input_config"] = new Dictionary<string, object?>
            {
                ["requests"] = new Dictionary<string, object?>
                {
                    ["requests"] = request.Requests.Select(item => new Dictionary<string, object?>
                    {
                        ["request"] = GeminiClient.BuildGeminiPayload(item.Request),
                        ["metadata"] = item.Metadata
                    }).ToArray()
                }
            }
        };

        if (request.Priority.HasValue)
        {
            batch["priority"] = request.Priority.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new Dictionary<string, object?>
        {
            ["batch"] = batch
        };
    }

    private static ModelBatchJob ParseBatchJob(JsonElement root)
    {
        var job = new ModelBatchJob
        {
            Name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
            Done = root.TryGetProperty("done", out var doneElement) && doneElement.GetBoolean(),
            State = TryGetState(root)
        };

        if (TryGetInlineResponses(root, out var inlineResponses))
        {
            job.InlineResponses = inlineResponses;
        }

        return job;
    }

    private static string? TryGetState(JsonElement root)
    {
        if (root.TryGetProperty("metadata", out var metadata) &&
            metadata.ValueKind == JsonValueKind.Object &&
            metadata.TryGetProperty("state", out var metadataState))
        {
            return metadataState.GetString();
        }

        if (root.TryGetProperty("response", out var response) &&
            response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("state", out var responseState))
        {
            return responseState.GetString();
        }

        return null;
    }

    private static bool TryGetInlineResponses(
        JsonElement root,
        out List<ModelBatchInlineResponse> inlineResponses)
    {
        inlineResponses = [];
        if (!root.TryGetProperty("response", out var response) ||
            !TryGetInlineResponseItems(response, out var responseItems))
        {
            return false;
        }

        foreach (var item in responseItems.EnumerateArray())
        {
            var batchResponse = new ModelBatchInlineResponse
            {
                Metadata = ParseMetadata(item)
            };

            if (item.TryGetProperty("response", out var generateContentResponse))
            {
                batchResponse.Response = ParseGenerateContentResponse(generateContentResponse);
            }
            else if (item.TryGetProperty("error", out var error))
            {
                batchResponse.ErrorMessage = error.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : error.ToString();
            }

            inlineResponses.Add(batchResponse);
        }

        return true;
    }

    private static bool TryGetInlineResponseItems(
        JsonElement response,
        out JsonElement responseItems)
    {
        if (TryGetInlineResponseArray(response, out responseItems))
        {
            return true;
        }

        if (response.TryGetProperty("output", out var output) &&
            TryGetInlineResponseArray(output, out responseItems))
        {
            return true;
        }

        responseItems = default;
        return false;
    }

    private static bool TryGetInlineResponseArray(
        JsonElement container,
        out JsonElement responseItems)
    {
        responseItems = default;
        if (!container.TryGetProperty("inlinedResponses", out var inlinedResponses))
        {
            return false;
        }

        if (inlinedResponses.ValueKind == JsonValueKind.Array)
        {
            responseItems = inlinedResponses;
            return true;
        }

        if (inlinedResponses.ValueKind == JsonValueKind.Object &&
            inlinedResponses.TryGetProperty("inlinedResponses", out var nestedResponses) &&
            nestedResponses.ValueKind == JsonValueKind.Array)
        {
            responseItems = nestedResponses;
            return true;
        }

        return false;
    }

    private static GeminiResponse ParseGenerateContentResponse(JsonElement generateContentResponse)
    {
        var textParts = new List<string>();
        if (generateContentResponse.TryGetProperty("candidates", out var candidates))
        {
            var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
            if (firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                    {
                        textParts.Add(text.GetString() ?? string.Empty);
                    }
                }
            }
        }

        return new GeminiResponse
        {
            Success = true,
            Content = string.Join("", textParts),
            TokenUsage = generateContentResponse.TryGetProperty("usageMetadata", out var usageMetadata)
                ? ParseTokenUsage(usageMetadata)
                : null
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

    private static Dictionary<string, object?> ParseMetadata(JsonElement item)
    {
        if (!item.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(metadata.GetRawText(), JsonOptions) ?? [];
    }

    private static string NormalizeModelName(string modelName)
    {
        return modelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelName["models/".Length..]
            : modelName;
    }
}
