using System.Threading.Channels;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// In-memory process queue for verified Gemini Batch API webhook notifications.
/// </summary>
public class GeminiBatchWebhookQueue : IGeminiBatchWebhookQueue
{
    private readonly Channel<GeminiBatchWebhookNotification> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiBatchWebhookQueue"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public GeminiBatchWebhookQueue(IConfiguration configuration)
    {
        var capacity = Math.Max(1, configuration.GetValue<int?>("Gemini:Webhooks:QueueCapacity") ?? 100);
        _channel = Channel.CreateBounded<GeminiBatchWebhookNotification>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc/>
    public async ValueTask EnqueueAsync(
        GeminiBatchWebhookNotification notification,
        CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(notification, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<GeminiBatchWebhookNotification> DequeueAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken);
}
