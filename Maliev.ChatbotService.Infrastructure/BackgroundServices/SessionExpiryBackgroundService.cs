using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that closes expired sessions and generates summaries.
/// </summary>
public class SessionExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionExpiryBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExpiryBackgroundService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public SessionExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SessionExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Executes the background service.
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session Expiry Background Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired sessions");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Session Expiry Background Service is stopping");
    }

    private async Task ProcessExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var summaryService = scope.ServiceProvider.GetRequiredService<IConversationSummaryService>();
        var summaryBatchService = scope.ServiceProvider.GetRequiredService<IConversationSummaryBatchService>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var metrics = scope.ServiceProvider.GetRequiredService<IConversationMetrics>();

        await summaryBatchService.ProcessOpenBatchesAsync(cancellationToken);

        // Get all expired sessions
        var expiredSessions = await sessionRepository.GetExpiredSessionsAsync(cancellationToken);

        if (expiredSessions.Count == 0)
        {
            _logger.LogDebug("No expired sessions found");
            return;
        }

        _logger.LogInformation("Processing {Count} expired sessions", expiredSessions.Count);
        var batchDeferredSessionIds = await summaryBatchService.SubmitExpiredSessionSummariesAsync(
            expiredSessions,
            cancellationToken);

        foreach (var session in expiredSessions)
        {
            try
            {
                // Check whether the session has messages without loading message entities.
                var messageCount = await messageRepository.CountBySessionIdAsync(session.Id, cancellationToken);

                if (messageCount > 0)
                {
                    if (batchDeferredSessionIds.Contains(session.Id))
                    {
                        session.Status = SessionStatus.Closed;
                        await sessionRepository.UpdateAsync(session, cancellationToken);
                        _logger.LogInformation(
                            "Queued batch summary and closed expired session {SessionId}",
                            session.Id);
                    }
                    else
                    {
                        // Generate summary for the session
                        await summaryService.GenerateSummaryAsync(session.Id, cancellationToken);
                        _logger.LogInformation("Generated summary for expired session {SessionId}", session.Id);
                    }
                }
                else
                {
                    // No messages, just close the session without summary
                    session.Status = SessionStatus.Closed;
                    await sessionRepository.UpdateAsync(session, cancellationToken);
                    _logger.LogInformation("Closed expired session {SessionId} without summary (no messages)", session.Id);
                }

                // Publish ChatbotSessionClosedEvent
                await eventPublisher.PublishAsync(new ChatbotSessionClosedEvent
                {
                    MessageId = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Source = "ChatbotService",
                    CorrelationId = session.Id,
                    SessionId = session.Id,
                    UserProfileId = session.UserProfileId,
                    Channel = session.Channel.ToString(),
                    StartTime = session.StartTime,
                    EndTime = DateTimeOffset.UtcNow,
                    TotalMessageCount = messageCount,
                    ClosureReason = "SessionExpired"
                }, cancellationToken);

                _logger.LogInformation("Published ChatbotSessionClosedEvent for session {SessionId}", session.Id);

                // Update active sessions count metric
                var activeSessionsCount = await sessionRepository.GetActiveSessionsCountAsync(cancellationToken);
                metrics.UpdateActiveSessionsCount(activeSessionsCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired session {SessionId}", session.Id);
            }
        }
    }
}
