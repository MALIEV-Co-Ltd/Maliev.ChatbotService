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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final response with accumulated thinking steps.</returns>
    public async Task<AgentChatResult> ExecuteAsync(
        GeminiRequest request,
        Func<ThinkingStep, Task>? onThinkingStep = null,
        string? userToken = null,
        CancellationToken cancellationToken = default)
    {
        var thinkingSteps = new List<ThinkingStep>();
        var stepNumber = 0;
        var messages = new List<GeminiMessage>(request.Messages);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var iterationRequest = new GeminiRequest
            {
                ModelName = request.ModelName,
                SystemInstruction = request.SystemInstruction,
                Messages = messages,
                TimeoutSeconds = 30,
                Tools = request.Tools,
                ToolConfig = request.ToolConfig
            };

            var response = await _geminiClient.SendMessageAsync(iterationRequest, cancellationToken);

            if (!response.Success)
            {
                return new AgentChatResult
                {
                    Success = false,
                    Content = response.Content,
                    ErrorMessage = response.ErrorMessage,
                    ThinkingSteps = thinkingSteps,
                    TokenUsage = response.TokenUsage
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
                    TokenUsage = response.TokenUsage
                };
            }

            // Add model's function call turn as an assistant message
            messages.Add(new GeminiMessage
            {
                Role = "assistant",
                Content = JsonSerializer.Serialize(response.FunctionCalls.Select(fc => new { functionCall = new { name = fc.Name, args = fc.Args } }))
            });

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

                // Execute the tool
                var sw = Stopwatch.StartNew();
                string toolResult;
                try
                {
                    toolResult = await _toolExecutor.ExecuteAsync(functionCall.Name, functionCall.Args, userToken, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed for {ToolName}", functionCall.Name);
                    toolResult = JsonSerializer.Serialize(new { error = $"Tool execution failed: {ex.Message}" });
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

                // Add function response as user message for next iteration
                var functionResultMessage = new GeminiMessage
                {
                    Role = "user",
                    Content = $"[Function result for {functionCall.Name}]: {toolResult}"
                };

                // Check for file attachments in tool result
                try
                {
                    using var doc = JsonDocument.Parse(toolResult);
                    if (doc.RootElement.TryGetProperty("_metadata", out var metadata))
                    {
                        if (metadata.TryGetProperty("is_file", out var isFile) && isFile.GetBoolean())
                        {
                            var mimeType = metadata.GetProperty("mime_type").GetString() ?? "application/octet-stream";
                            var data = metadata.GetProperty("data").GetString() ?? string.Empty;

                            functionResultMessage.Attachments ??= new List<GeminiAttachment>();
                            functionResultMessage.Attachments.Add(new GeminiAttachment
                            {
                                ContentType = mimeType,
                                MimeType = mimeType,
                                Data = data
                            });

                            // Update content to not include the heavy base64 data in the text part if possible,
                            // or just keep it minimal.
                            functionResultMessage.Content = $"[Function result for {functionCall.Name}]: Document data attached.";
                        }
                    }
                }
                catch
                {
                    // Not a JSON or doesn't have metadata, ignore
                }

                messages.Add(functionResultMessage);
            }
        }

        _logger.LogWarning("Agent loop reached maximum iterations ({Max})", MaxIterations);
        return new AgentChatResult
        {
            Success = true,
            Content = "I apologize, but I was unable to complete the research within the allowed number of steps. Please try a more specific question.",
            ThinkingSteps = thinkingSteps
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
    /// <summary>Accumulated thinking steps from the agent loop.</summary>
    public List<ThinkingStep> ThinkingSteps { get; set; } = new();
    /// <summary>Token usage from the last Gemini call.</summary>
    public GeminiTokenUsage? TokenUsage { get; set; }
}
