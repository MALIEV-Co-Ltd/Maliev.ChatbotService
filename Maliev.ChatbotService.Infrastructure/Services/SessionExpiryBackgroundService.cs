using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Background service that periodically processes expired sessions and updates active session metrics.
/// </summary>
public class SessionExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConversationMetrics _metrics;
    private readonly ILogger<SessionExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExpiryBackgroundService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="metrics">The conversation metrics.</param>
    /// <param name="logger">The logger.</param>
    public SessionExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ConversationMetrics metrics,
        ILogger<SessionExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionExpiryBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredSessionsAndUpdateMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired sessions");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("SessionExpiryBackgroundService stopped");
    }

    /// <summary>
    /// Processes expired sessions and updates the active sessions count metric.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ProcessExpiredSessionsAndUpdateMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionExpiryService = scope.ServiceProvider.GetRequiredService<ISessionExpiryService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();

        // Process expired sessions
        await sessionExpiryService.ProcessExpiredSessionsAsync(cancellationToken);

        // Update active sessions count metric
        var activeSessionsCount = await dbContext.ConversationSessions
            .CountAsync(s => s.Status == SessionStatus.Active, cancellationToken);

        _metrics.UpdateActiveSessionsCount(activeSessionsCount);

        _logger.LogDebug("Updated active sessions count: {Count}", activeSessionsCount);
    }
}
