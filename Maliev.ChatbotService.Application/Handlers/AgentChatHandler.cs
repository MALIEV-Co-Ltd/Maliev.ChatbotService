using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Orchestrates multi-turn function calling loop with Gemini.
/// </summary>
public class AgentChatHandler
{
    private readonly IGeminiClient _geminiClient;
    private readonly IToolExecutorService _toolExecutor;
    private readonly ILogger<AgentChatHandler> _logger;
    private const int MaxIterations = 10;
    private const int MaxCallsPerTool = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentChatHandler"/> class.
    /// </summary>
    public AgentChatHandler(
        IGeminiClient geminiClient,
        IToolExecutorService toolExecutor,
        ILogger<AgentChatHandler> logger)
    {
        _geminiClient = geminiClient;
        _toolExecutor = toolExecutor;
        _logger = logger;
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

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var iterationRequest = new GeminiRequest
            {
                ModelName = request.ModelName,
                SystemInstruction = request.SystemInstruction,
                Messages = messages,
                TimeoutSeconds = 30,
                Tools = request.Tools,
                ToolConfig = request.ToolConfig,
                IncludeThoughts = request.IncludeThoughts
            };

            var response = await SendGeminiMaybeStreamingAsync(iterationRequest, onTextDelta, onThoughtDelta, cancellationToken);

            if (response.TokenUsage is { } usage)
            {
                accumulatedUsage.PromptTokens += usage.PromptTokens;
                accumulatedUsage.CompletionTokens += usage.CompletionTokens;
                accumulatedUsage.TotalTokens += usage.TotalTokens;
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
                    TokenUsage = sawUsage ? accumulatedUsage : null
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
                    TokenUsage = sawUsage ? accumulatedUsage : null
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
                        resultAttachments.Add(new GeminiAttachment
                        {
                            ContentType = mimeType,
                            MimeType = mimeType,
                            Data = data
                        });

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
            TokenUsage = sawUsage ? accumulatedUsage : null
        };
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
}
