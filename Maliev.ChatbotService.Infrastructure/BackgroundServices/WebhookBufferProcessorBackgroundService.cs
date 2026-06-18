using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.BackgroundServices;

/// <summary>
/// Polls the durable webhook buffer for due sessions and processes each in its own DI scope (S6). This
/// replaces the previous fire-and-forget <c>Task.Run</c> debounce: because the queue is Redis-backed
/// and uses a lease (visibility timeout), a crash mid-processing no longer drops work — the lease
/// expires and a later poll (in this process or after a restart) reclaims the session.
/// </summary>
public class WebhookBufferProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebhookBufferQueue _queue;
    private readonly ILogger<WebhookBufferProcessorBackgroundService> _logger;
    private readonly TimeSpan _pollInterval;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookBufferProcessorBackgroundService"/> class.
    /// </summary>
    public WebhookBufferProcessorBackgroundService(
        IServiceProvider serviceProvider,
        IWebhookBufferQueue queue,
        IConfiguration configuration,
        ILogger<WebhookBufferProcessorBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(configuration.GetValue<double?>("Webhook:PollIntervalSeconds") ?? 1);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Buffer Processor Background Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var due = await _queue.ClaimDueSessionsAsync(DateTimeOffset.UtcNow, stoppingToken);

                foreach (var sessionId in due)
                {
                    // Fresh scope per session so each gets its own DbContext (no cross-session state bleed).
                    using var scope = _serviceProvider.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IWebhookBufferProcessor>();
                    await processor.ProcessSessionAsync(sessionId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling the webhook buffer");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Webhook Buffer Processor Background Service is stopping");
    }
}
