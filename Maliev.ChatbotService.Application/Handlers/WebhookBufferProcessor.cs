using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Processes a single due session from the durable webhook buffer (S6): peeks the buffered batch, runs
/// it through <see cref="SendMessageCommandHandler"/>, replies on the originating platform, then
/// acknowledges. A handler failure is reported for retry (the buffer is kept); a reply failure is
/// best-effort only — the expensive turn already succeeded and is persisted, so it is never re-run.
/// </summary>
public class WebhookBufferProcessor : IWebhookBufferProcessor
{
    private readonly IWebhookBufferQueue _queue;
    private readonly SendMessageCommandHandler _sendMessageHandler;
    private readonly ILineClient _lineClient;
    private readonly IMetaClient _metaClient;
    private readonly ILogger<WebhookBufferProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookBufferProcessor"/> class.
    /// </summary>
    public WebhookBufferProcessor(
        IWebhookBufferQueue queue,
        SendMessageCommandHandler sendMessageHandler,
        ILineClient lineClient,
        IMetaClient metaClient,
        ILogger<WebhookBufferProcessor> logger)
    {
        _queue = queue;
        _sendMessageHandler = sendMessageHandler;
        _lineClient = lineClient;
        _metaClient = metaClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var batch = await _queue.PeekAsync(sessionId, cancellationToken);
        if (batch is null)
        {
            return;
        }

        SendMessageResult result;
        try
        {
            _logger.LogInformation(
                "Processing {Count} buffered webhook message(s) for session {SessionId}",
                batch.MessageCount, sessionId);

            result = await _sendMessageHandler.HandleAsync(
                new SendMessageCommand { SessionId = batch.SessionId, Content = batch.CombinedContent },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Handler failure (e.g. provider down, lock contention): keep the buffer and retry.
            _logger.LogError(ex, "Failed to process buffered webhook for session {SessionId}; scheduling retry", sessionId);
            await _queue.ReportFailureAsync(sessionId, cancellationToken);
            return;
        }

        // The turn succeeded and is persisted; reply is best-effort and must not re-run the turn.
        await SendReplyAsync(batch, result.Content, cancellationToken);
        await _queue.AcknowledgeAsync(sessionId, batch.MessageCount, cancellationToken);
    }

    private async Task SendReplyAsync(BufferedWebhookBatch batch, string content, CancellationToken cancellationToken)
    {
        try
        {
            switch (batch.Channel)
            {
                case Channel.Line:
                    if (string.IsNullOrEmpty(batch.ReplyToken))
                    {
                        _logger.LogWarning("LINE reply token missing for session {SessionId}; skipping reply", batch.SessionId);
                        return;
                    }

                    await _lineClient.SendTextMessageAsync(batch.ReplyToken, content, cancellationToken);
                    break;

                case Channel.Facebook:
                case Channel.Instagram:
                case Channel.WhatsApp:
                    if (string.IsNullOrEmpty(batch.RecipientId))
                    {
                        _logger.LogWarning("Meta recipient ID missing for session {SessionId}; skipping reply", batch.SessionId);
                        return;
                    }

                    await _metaClient.SendTextMessageAsync(batch.RecipientId, content, batch.Channel.ToString(), cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unsupported channel {Channel} for webhook reply (session {SessionId})", batch.Channel, batch.SessionId);
                    break;
            }
        }
        catch (Exception ex)
        {
            // A stale LINE reply token or a transient send error must not trigger a costly re-run of the
            // turn — log and move on. (A future push-API path can use the persisted PlatformUserId.)
            _logger.LogWarning(ex, "Failed to deliver webhook reply for session {SessionId}", batch.SessionId);
        }
    }
}
