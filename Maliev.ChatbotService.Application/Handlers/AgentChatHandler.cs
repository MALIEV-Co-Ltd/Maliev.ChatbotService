using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Orchestrates multi-turn function calling loop with Gemini.
/// </summary>
public class AgentChatHandler
{
    private const long DefaultFileApiInlineThresholdBytes = 5L * 1024 * 1024;
    private const int MaxIterations = 10;
    private const int MaxCallsPerTool = 3;

    private readonly IGeminiClient _geminiClient;
    private readonly IToolExecutorService _toolExecutor;
    private readonly ILogger<AgentChatHandler> _logger;
    private readonly IModelFileStagingService? _modelFileStagingService;
    private readonly long _fileApiInlineThresholdBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentChatHandler"/> class.
    /// </summary>
    public AgentChatHandler(
        IGeminiClient geminiClient,
        IToolExecutorService toolExecutor,
        ILogger<AgentChatHandler> logger,
        IModelFileStagingService? modelFileStagingService = null,
        IConfiguration? configuration = null)
    {
        _geminiClient = geminiClient;
        _toolExecutor = toolExecutor;
        _logger = logger;
        _modelFileStagingService = modelFileStagingService;
        _fileApiInlineThresholdBytes = Math.Max(
            0,
            configuration?.GetValue<long?>("Gemini:FileApiInlineThresholdBytes") ??
                DefaultFileApiInlineThresholdBytes);
    }

    /// <summary>
    /// Executes an agent chat loop with function calling.
    /// </summary>
    /// <param name="request">The initial Gemini request with tools.</param>
    /// <param name="onThinkingStep">Callback for each thinking step (for real-time streaming).</param>
    /// <param name="userToken">The Bearer token to forward to downstream tool calls, or null if unavailable.</param>
    /// <param name="quoteAgentContextToken">Signed QuoteEngine agent context token for QuoteEngine tool calls.</param>
    /// <param name="onTextDelta">Callback for generated assistant text deltas.</param>
    /// <param name="onThoughtDelta">Callback for streamed model reasoning (thought) deltas.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final response with accumulated thinking steps.</returns>
    public async Task<AgentChatResult> ExecuteAsync(
        GeminiRequest request,
        Func<ThinkingStep, Task>? onThinkingStep = null,
        string? userToken = null,
        string? quoteAgentContextToken = null,
        Func<string, Task>? onTextDelta = null,
        Func<string, Task>? onThoughtDelta = null,
        CancellationToken cancellationToken = default)
    {
        var thinkingSteps = new List<ThinkingStep>();
        var stepNumber = 0;
        var messages = new List<GeminiMessage>(request.Messages);

        // Per-turn guard against a model repeatedly calling the same tool (C5). Persists across
        // iterations of this turn so a confused model cannot burn downstream calls/cost.
        var toolCallCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        // Accumulate token usage across every iteration of the loop, not just the last call. One agent
        // turn can fan out to MaxIterations model calls, so reporting only the final call's usage would
        // grossly undercount the turn and defeat the daily token budget (S2) on the agent path.
        var accumulatedUsage = new GeminiTokenUsage();
        var sawUsage = false;
        string? serviceTier = null;
        var groundingWebSearchQueries = new List<string>();
        var stagedFileNames = new List<string>();

        try
        {
            for (var iteration = 0; iteration < MaxIterations; iteration++)
            {
                var iterationRequest = new GeminiRequest
                {
                    ModelName = request.ModelName,
                    SystemInstruction = request.SystemInstruction,
                    Messages = messages,
                    TimeoutSeconds = 30,
                    MaxTokens = request.MaxTokens,
                    MaxPromptTokens = request.MaxPromptTokens,
                    Tools = request.Tools,
                    ToolConfig = request.ToolConfig,
                    IncludeThoughts = request.IncludeThoughts,
                    ThinkingBudget = request.ThinkingBudget,
                    MediaResolution = request.MediaResolution,
                    CachedContentName = request.CachedContentName,
                    EnableWebSearch = request.EnableWebSearch,
                    EnableUrlContext = request.EnableUrlContext,
                    ServiceTier = request.ServiceTier,
                    Store = request.Store
                };

                var response = await SendGeminiMaybeStreamingAsync(iterationRequest, onTextDelta, onThoughtDelta, cancellationToken);
                serviceTier = response.ServiceTier ?? serviceTier;
                AddGroundingWebSearchQueries(groundingWebSearchQueries, response.GroundingWebSearchQueries);

                if (response.TokenUsage is { } usage)
                {
                    accumulatedUsage.PromptTokens += usage.PromptTokens;
                    accumulatedUsage.CompletionTokens += usage.CompletionTokens;
                    accumulatedUsage.CachedPromptTokens += usage.CachedPromptTokens;
                    accumulatedUsage.ToolUsePromptTokens += usage.ToolUsePromptTokens;
                    accumulatedUsage.ThoughtTokens += usage.ThoughtTokens;
                    accumulatedUsage.TotalTokens += usage.TotalTokens;
                    AddTokenDetails(accumulatedUsage.PromptTokenDetails, usage.PromptTokenDetails);
                    AddTokenDetails(accumulatedUsage.CachedTokenDetails, usage.CachedTokenDetails);
                    AddTokenDetails(accumulatedUsage.CandidateTokenDetails, usage.CandidateTokenDetails);
                    AddTokenDetails(accumulatedUsage.ToolUsePromptTokenDetails, usage.ToolUsePromptTokenDetails);
                    sawUsage = true;
                }

                if (!response.Success)
                {
                    return new AgentChatResult
                    {
                        Success = false,
                        Content = response.Content,
                        ErrorMessage = response.ErrorMessage,
                        IsFallback = response.IsFallback,
                        ThinkingSteps = thinkingSteps,
                        TokenUsage = sawUsage ? accumulatedUsage : null,
                        ServiceTier = serviceTier,
                        GroundingWebSearchQueries = groundingWebSearchQueries
                    };
                }

                if (!response.HasFunctionCalls)
                {
                    // Final text response
                    return new AgentChatResult
                    {
                        Success = true,
                        Content = response.Content,
                        ThinkingSteps = thinkingSteps,
                        TokenUsage = sawUsage ? accumulatedUsage : null,
                        ServiceTier = serviceTier,
                        GroundingWebSearchQueries = groundingWebSearchQueries
                    };
                }

                // Add the model's tool-call turn as native function-call parts (not serialized text).
                messages.Add(new GeminiMessage
                {
                    Role = "assistant",
                    FunctionCalls = response.FunctionCalls
                });

                var functionResponses = new List<GeminiFunctionResponse>();
                List<GeminiAttachment>? resultAttachments = null;

                // Process each function call
                foreach (var functionCall in response.FunctionCalls)
                {
                    stepNumber++;
                    var callStep = new ThinkingStep
                    {
                        StepNumber = stepNumber,
                        Type = "function_call",
                        Title = $"Calling {functionCall.Name}...",
                        Detail = $"Arguments: {JsonSerializer.Serialize(functionCall.Args)}",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    thinkingSteps.Add(callStep);
                    if (onThinkingStep != null) await onThinkingStep(callStep);

                    // Execute the tool, unless this tool already hit its per-turn call limit (C5).
                    var sw = Stopwatch.StartNew();
                    string toolResult;
                    toolCallCounts.TryGetValue(functionCall.Name, out var priorCalls);
                    if (priorCalls >= MaxCallsPerTool)
                    {
                        _logger.LogWarning(
                            "Tool {ToolName} reached its per-turn call limit ({Max}); skipping execution.",
                            functionCall.Name,
                            MaxCallsPerTool);
                        toolResult = JsonSerializer.Serialize(new
                        {
                            error = $"Tool '{functionCall.Name}' has already been called {priorCalls} times in this turn. " +
                                "Do not call it again; use the information you already have to answer the customer."
                        });
                    }
                    else
                    {
                        toolCallCounts[functionCall.Name] = priorCalls + 1;
                        try
                        {
                            if (string.IsNullOrWhiteSpace(quoteAgentContextToken))
                            {
                                toolResult = await _toolExecutor.ExecuteAsync(functionCall.Name, functionCall.Args, userToken, cancellationToken);
                            }
                            else
                            {
                                var context = new ToolExecutionContext(userToken, quoteAgentContextToken);
                                toolResult = await _toolExecutor.ExecuteAsync(functionCall.Name, functionCall.Args, context, cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Tool execution failed for {ToolName}", functionCall.Name);
                            toolResult = JsonSerializer.Serialize(new { error = $"Tool execution failed: {ex.Message}" });
                        }
                    }
                    sw.Stop();

                    stepNumber++;
                    var resultStep = new ThinkingStep
                    {
                        StepNumber = stepNumber,
                        Type = "function_result",
                        Title = $"Got result from {functionCall.Name}",
                        Detail = toolResult.Length > 500 ? toolResult[..500] + "..." : toolResult,
                        Timestamp = DateTimeOffset.UtcNow,
                        DurationMs = sw.ElapsedMilliseconds,
                        Data = toolResult
                    };
                    thinkingSteps.Add(resultStep);
                    if (onThinkingStep != null) await onThinkingStep(resultStep);

                    // Move any file payload out of the function response and into a media attachment so
                    // the next turn carries the document/image as a real part, not heavy inline JSON.
                    var responseJson = toolResult;
                    try
                    {
                        using var doc = JsonDocument.Parse(toolResult);
                        if (doc.RootElement.TryGetProperty("_metadata", out var metadata) &&
                            metadata.TryGetProperty("is_file", out var isFile) && isFile.GetBoolean())
                        {
                            var mimeType = metadata.GetProperty("mime_type").GetString() ?? "application/octet-stream";
                            var data = metadata.GetProperty("data").GetString() ?? string.Empty;

                            resultAttachments ??= new List<GeminiAttachment>();
                            resultAttachments.Add(await BuildToolResultAttachmentAsync(
                                functionCall.Name,
                                mimeType,
                                data,
                                stagedFileNames,
                                cancellationToken));

                            responseJson = JsonSerializer.Serialize(new { status = "ok", message = "Document data attached as a separate part." });
                        }
                    }
                    catch
                    {
                        // Not JSON or no metadata; send the raw tool result as the response.
                    }

                    functionResponses.Add(new GeminiFunctionResponse
                    {
                        Name = functionCall.Name,
                        Id = functionCall.Id,
                        ResponseJson = responseJson
                    });
                }

                // Send all tool results back in a single function-response turn.
                messages.Add(new GeminiMessage
                {
                    Role = "user",
                    FunctionResponses = functionResponses,
                    Attachments = resultAttachments
                });
            }

            _logger.LogWarning("Agent loop reached maximum iterations ({Max})", MaxIterations);
            return new AgentChatResult
            {
                Success = true,
                Content = "I wasn't able to fully work through that request in the steps available. Could you share a bit more detail, or break it into a smaller step? You can also reach the MALIEV team at info@maliev.com.",
                ThinkingSteps = thinkingSteps,
                TokenUsage = sawUsage ? accumulatedUsage : null,
                ServiceTier = serviceTier,
                GroundingWebSearchQueries = groundingWebSearchQueries
            };
        }
        finally
        {
            await DeleteStagedFilesAsync(stagedFileNames);
        }
    }

    private async Task<GeminiAttachment> BuildToolResultAttachmentAsync(
        string toolName,
        string mimeType,
        string data,
        ICollection<string> stagedFileNames,
        CancellationToken cancellationToken)
    {
        if (ShouldStageToolFile(data, out var decodedBytes))
        {
            var stagedFile = await TryStageToolFileAsync(toolName, mimeType, decodedBytes, cancellationToken);
            if (stagedFile is not null)
            {
                if (!string.IsNullOrWhiteSpace(stagedFile.Name))
                {
                    stagedFileNames.Add(stagedFile.Name);
                }

                return new GeminiAttachment
                {
                    ContentType = mimeType,
                    MimeType = string.IsNullOrWhiteSpace(stagedFile.MimeType) ? mimeType : stagedFile.MimeType,
                    Data = stagedFile.FileUri
                };
            }
        }

        return new GeminiAttachment
        {
            ContentType = mimeType,
            MimeType = mimeType,
            Data = data
        };
    }

    private async Task<ModelFileReference?> TryStageToolFileAsync(
        string toolName,
        string mimeType,
        byte[] decodedBytes,
        CancellationToken cancellationToken)
    {
        if (_modelFileStagingService is null)
        {
            return null;
        }

        try
        {
            return await _modelFileStagingService.StageFileAsync(
                new ModelFileStagingRequest
                {
                    FileName = BuildToolResultFileName(toolName, mimeType),
                    MimeType = mimeType,
                    Content = decodedBytes
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Gemini file staging failed for tool result {ToolName}; falling back to inline payload.",
                toolName);
            return null;
        }
    }

    private async Task DeleteStagedFilesAsync(IReadOnlyCollection<string> stagedFileNames)
    {
        if (_modelFileStagingService is null || stagedFileNames.Count == 0)
        {
            return;
        }

        foreach (var fileName in stagedFileNames
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal))
        {
            try
            {
                await _modelFileStagingService.DeleteFileAsync(fileName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Gemini staged file cleanup failed for tool-result attachment {FileName}.",
                    fileName);
            }
        }
    }

    private bool ShouldStageToolFile(string base64Data, out byte[] decodedBytes)
    {
        decodedBytes = [];
        if (_modelFileStagingService is null || _fileApiInlineThresholdBytes <= 0)
        {
            return false;
        }

        var base64Payload = NormalizeBase64Payload(base64Data);
        if (!TryGetBase64DecodedLength(base64Payload, out var decodedLength) ||
            decodedLength < _fileApiInlineThresholdBytes)
        {
            return false;
        }

        try
        {
            decodedBytes = Convert.FromBase64String(base64Payload);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildToolResultFileName(string toolName, string mimeType)
    {
        var sanitizedToolName = new string(toolName
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedToolName))
        {
            sanitizedToolName = "tool";
        }

        return $"tool-result-{sanitizedToolName}{ResolveFileExtension(mimeType)}";
    }

    private static string ResolveFileExtension(string mimeType) =>
        mimeType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "audio/mpeg" => ".mp3",
            _ => string.Empty
        };

    private static string NormalizeBase64Payload(string base64Data)
    {
        var payloadStart = base64Data.IndexOf(',', StringComparison.Ordinal);
        return payloadStart >= 0 ? base64Data[(payloadStart + 1)..] : base64Data;
    }

    private static bool TryGetBase64DecodedLength(string base64Payload, out long decodedLength)
    {
        var payload = new string(base64Payload.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (payload.Length == 0 || payload.Length % 4 != 0)
        {
            decodedLength = 0;
            return false;
        }

        var padding = payload.EndsWith("==", StringComparison.Ordinal)
            ? 2
            : payload.EndsWith("=", StringComparison.Ordinal) ? 1 : 0;
        decodedLength = (payload.Length / 4L * 3L) - padding;
        return decodedLength >= 0;
    }

    private async Task<GeminiResponse> SendGeminiMaybeStreamingAsync(
        GeminiRequest request,
        Func<string, Task>? onTextDelta,
        Func<string, Task>? onThoughtDelta,
        CancellationToken cancellationToken)
    {
        if (onTextDelta == null && onThoughtDelta == null)
        {
            return await _geminiClient.SendMessageAsync(request, cancellationToken);
        }

        GeminiResponse? finalResponse = null;
        try
        {
            await foreach (var streamEvent in _geminiClient.StreamMessageAsync(request, cancellationToken))
            {
                if (onTextDelta != null &&
                    streamEvent.Type.Equals("delta", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(streamEvent.Delta))
                {
                    await onTextDelta(streamEvent.Delta);
                }
                else if (streamEvent.Type.Equals("thought", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(streamEvent.Thought) && onThoughtDelta != null)
                {
                    await onThoughtDelta(streamEvent.Thought);
                }
                else if (streamEvent.Type.Equals("final", StringComparison.OrdinalIgnoreCase))
                {
                    finalResponse = streamEvent.Response;
                }
                else if (streamEvent.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    finalResponse = new GeminiResponse
                    {
                        Success = false,
                        ErrorMessage = streamEvent.ErrorMessage ?? "Gemini streaming failed"
                    };
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new GeminiResponse
            {
                Success = false,
                ErrorMessage = "Gemini streaming failed"
            };
        }

        return finalResponse ?? new GeminiResponse
        {
            Success = false,
            ErrorMessage = "Gemini streaming ended without a final response."
        };
    }

    private static void AddTokenDetails(
        List<GeminiModalityTokenCount> target,
        IReadOnlyCollection<GeminiModalityTokenCount> source)
    {
        foreach (var detail in source)
        {
            target.Add(new GeminiModalityTokenCount
            {
                Modality = detail.Modality,
                TokenCount = detail.TokenCount
            });
        }
    }

    private static void AddGroundingWebSearchQueries(List<string> target, IReadOnlyCollection<string> source)
    {
        foreach (var query in source)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                target.Add(query);
            }
        }
    }
}

/// <summary>
/// Result of an agent chat execution.
/// </summary>
public class AgentChatResult
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }
    /// <summary>The final text content.</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Error message if the operation failed.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Whether the result is a graceful fallback from the AI provider.</summary>
    public bool IsFallback { get; set; }
    /// <summary>Accumulated thinking steps from the agent loop.</summary>
    public List<ThinkingStep> ThinkingSteps { get; set; } = new();
    /// <summary>Token usage summed across every Gemini call made during the agent loop, or null if the provider reported none.</summary>
    public GeminiTokenUsage? TokenUsage { get; set; }
    /// <summary>Gemini response service tier reported by the provider, when available.</summary>
    public string? ServiceTier { get; set; }
    /// <summary>Gemini Google Search grounding queries reported across provider calls.</summary>
    public List<string> GroundingWebSearchQueries { get; set; } = new();
}
