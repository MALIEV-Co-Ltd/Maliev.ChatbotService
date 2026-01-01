using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for managing response timeouts with configurable durations.
/// </summary>
public class ResponseTimeoutService : IResponseTimeoutService
{
    private readonly ILogger<ResponseTimeoutService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseTimeoutService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ResponseTimeoutService(ILogger<ResponseTimeoutService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<T?> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return await operation(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Operation timed out after {Timeout} seconds", timeoutSeconds);
            return default;
        }
    }

    /// <inheritdoc/>
    public Task<int> GetTimeoutAsync(string operationType, CancellationToken cancellationToken = default)
    {
        // Default timeout configuration based on operation type
        var timeout = operationType.ToLowerInvariant() switch
        {
            "ai_generation" => 30,
            "web_search" => 10,
            "database_query" => 5,
            _ => 15
        };

        return Task.FromResult(timeout);
    }
}
