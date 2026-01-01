namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for managing response timeouts.
/// </summary>
public interface IResponseTimeoutService
{
    /// <summary>
    /// Executes an operation with a timeout.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation, or default if timed out.</returns>
    Task<T?> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timeout duration for a specific operation type.
    /// </summary>
    /// <param name="operationType">The type of operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The timeout duration in seconds.</returns>
    Task<int> GetTimeoutAsync(string operationType, CancellationToken cancellationToken = default);
}
