using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.BackgroundServices;

/// <summary>
/// Processes verified Gemini Batch API webhook notifications outside the HTTP request path.
/// </summary>
public class GeminiBatchWebhookProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IGeminiBatchWebhookQueue _queue;
    private readonly ILogger<GeminiBatchWebhookProcessorBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiBatchWebhookProcessorBackgroundService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="queue">The verified Gemini batch webhook queue.</param>
    /// <param name="logger">The logger.</param>
    public GeminiBatchWebhookProcessorBackgroundService(
        IServiceProvider serviceProvider,
        IGeminiBatchWebhookQueue queue,
        ILogger<GeminiBatchWebhookProcessorBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gemini Batch Webhook Processor Background Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            GeminiBatchWebhookNotification notification;
            try
            {
                notification = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var batchService = scope.ServiceProvider.GetRequiredService<IConversationSummaryBatchService>();
                if (string.IsNullOrWhiteSpace(notification.BatchName))
                {
                    await batchService.ProcessOpenBatchesAsync(stoppingToken);
                }
                else
                {
                    await batchService.ProcessBatchAsync(notification.BatchName, stoppingToken);
                }

                _logger.LogInformation(
                    "Processed Gemini batch webhook {WebhookId} ({EventType}) for {BatchName}",
                    notification.WebhookId,
                    notification.EventType,
                    notification.BatchName ?? "unknown batch");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process Gemini batch webhook {WebhookId} ({EventType})",
                    notification.WebhookId,
                    notification.EventType);
            }
        }

        _logger.LogInformation("Gemini Batch Webhook Processor Background Service is stopping");
    }
}
