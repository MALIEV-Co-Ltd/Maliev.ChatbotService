using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler that requires trusted execution context beyond model-supplied arguments.
/// </summary>
public interface IContextualToolHandler : IToolHandler
{
    /// <summary>
    /// Executes the specified tool function with trusted caller context.
    /// </summary>
    Task<string> ExecuteAsync(
        string toolName,
        Dictionary<string, object> args,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}
