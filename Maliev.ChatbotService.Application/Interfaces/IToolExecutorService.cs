namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service for executing tool/function calls from the AI agent.
/// </summary>
public interface IToolExecutorService
{
    /// <summary>
    /// Executes a tool function by name with the given arguments.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute.</param>
    /// <param name="args">The arguments for the tool.</param>
    /// <param name="userToken">The Bearer token to forward to downstream services, or null if unavailable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JSON-serialized result of the tool execution.</returns>
    Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, string? userToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available tool declarations for Gemini function calling.
    /// </summary>
    List<GeminiToolDeclaration> GetToolDeclarations();
}
