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
    /// Executes a tool function by name with the given arguments and scoped execution context.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute.</param>
    /// <param name="args">The arguments for the tool.</param>
    /// <param name="context">Tool execution context supplied by the trusted caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The JSON-serialized result of the tool execution.</returns>
    Task<string> ExecuteAsync(
        string toolName,
        Dictionary<string, object> args,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available tool declarations for Gemini function calling.
    /// </summary>
    List<GeminiToolDeclaration> GetToolDeclarations();

    /// <summary>
    /// Gets the list of available tool declarations for a channel-specific profile.
    /// </summary>
    /// <param name="profile">The tool profile name.</param>
    List<GeminiToolDeclaration> GetToolDeclarations(string profile);
}

/// <summary>
/// Trusted execution context for agent tool calls.
/// </summary>
/// <param name="UserToken">Bearer token to forward to downstream services, if present.</param>
/// <param name="QuoteAgentContextToken">Signed QuoteEngine agent context token, if present.</param>
public sealed record ToolExecutionContext(string? UserToken, string? QuoteAgentContextToken);
