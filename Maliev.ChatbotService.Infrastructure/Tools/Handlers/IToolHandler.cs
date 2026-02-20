namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Interface for a tool handler that executes specific tool functions.
/// </summary>
public interface IToolHandler
{
    /// <summary>
    /// Executes the specified tool function.
    /// </summary>
    Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, string? userToken, CancellationToken cancellationToken);
}
