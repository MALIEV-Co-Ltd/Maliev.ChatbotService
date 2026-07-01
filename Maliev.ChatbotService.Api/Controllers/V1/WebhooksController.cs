using Asp.Versioning;
using Maliev.ChatbotService.Api.Models.Webhooks;
using Maliev.ChatbotService.Api.Security;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for handling webhook events from messaging platforms.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/webhooks")]
public class WebhooksController : ControllerBase
{
    private const string GeminiWebhookSigningSecretKey = "Gemini:Webhooks:SigningSecret";
    private static readonly DistributedCacheEntryOptions GeminiWebhookDedupeCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    };

    private readonly ProcessWebhookCommandHandler _webhookHandler;
    private readonly ILineClient _lineClient;
    private readonly IMetaClient _metaClient;
    private readonly IGeminiBatchWebhookQueue _geminiBatchWebhookQueue;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhooksController"/> class.
    /// </summary>
    /// <param name="webhookHandler">The webhook command handler.</param>
    /// <param name="lineClient">The LINE client.</param>
    /// <param name="metaClient">The Meta client.</param>
    /// <param name="geminiBatchWebhookQueue">The Gemini batch webhook queue.</param>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public WebhooksController(
        ProcessWebhookCommandHandler webhookHandler,
        ILineClient lineClient,
        IMetaClient metaClient,
        IGeminiBatchWebhookQueue geminiBatchWebhookQueue,
        IDistributedCache cache,
        IConfiguration configuration,
        ILogger<WebhooksController> logger)
    {
        _webhookHandler = webhookHandler;
        _lineClient = lineClient;
        _metaClient = metaClient;
        _geminiBatchWebhookQueue = geminiBatchWebhookQueue;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Handles LINE webhook events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK if successful.</returns>
    [HttpPost("line")]
    public async Task<IActionResult> HandleLineWebhook(CancellationToken cancellationToken)
    {
        // Verify signature
        var signature = Request.Headers["X-Line-Signature"].ToString();
        var requestBody = await ReadRequestBodyAsync();

        if (!_lineClient.VerifySignature(signature, requestBody))
        {
            _logger.LogWarning("Invalid LINE webhook signature");
            return Unauthorized(new { error = "Invalid signature" });
        }

        var @event = JsonSerializer.Deserialize<LineWebhookEvent>(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (@event == null) return BadRequest();

        // Process each event
        foreach (var lineEvent in @event.Events)
        {
            // Only process message events
            if (lineEvent.Type != "message" || lineEvent.Message?.Type != "text")
            {
                _logger.LogInformation("Skipping non-text message event: {EventType}", lineEvent.Type);
                continue;
            }

            var command = new ProcessWebhookCommand
            {
                Channel = Channel.Line,
                PlatformUserId = lineEvent.Source.UserId,
                MessageText = lineEvent.Message.Text ?? string.Empty,
                ReplyToken = lineEvent.ReplyToken,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(lineEvent.Timestamp)
            };

            try
            {
                await _webhookHandler.HandleAsync(command, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LINE webhook event");
                // Continue processing other events
            }
        }

        return Ok();
    }

    /// <summary>
    /// Handles Meta webhook verification challenge.
    /// </summary>
    /// <param name="mode">The mode parameter.</param>
    /// <param name="token">The verification token.</param>
    /// <param name="challenge">The challenge string.</param>
    /// <returns>The challenge string if verification succeeds.</returns>
    [HttpGet("meta")]
    public IActionResult HandleMetaWebhookVerification(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var verifyToken = _configuration["Meta:VerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Meta webhook verification succeeded");
            return Content(challenge ?? string.Empty);
        }

        _logger.LogWarning("Meta webhook verification failed");
        return Forbid();
    }

    /// <summary>
    /// Handles Meta webhook events (Facebook, Instagram, WhatsApp).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK if successful.</returns>
    [HttpPost("meta")]
    public async Task<IActionResult> HandleMetaWebhook(CancellationToken cancellationToken)
    {
        // Verify signature
        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        var requestBody = await ReadRequestBodyAsync();

        if (!_metaClient.VerifySignature(signature, requestBody))
        {
            _logger.LogWarning("Invalid Meta webhook signature");
            return Unauthorized(new { error = "Invalid signature" });
        }

        var @event = JsonSerializer.Deserialize<MetaWebhookEvent>(requestBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (@event == null) return BadRequest();

        // Process each entry
        foreach (var entry in @event.Entry)
        {
            // Get channel from object type
            var channel = GetChannelFromObject(@event.Object);

            // Process messaging events
            if (entry.Messaging != null)
            {
                foreach (var messaging in entry.Messaging)
                {
                    // Only process message events (not postback, read, delivery, etc.)
                    if (messaging.Message?.Text == null)
                    {
                        _logger.LogInformation("Skipping non-text message event");
                        continue;
                    }

                    var command = new ProcessWebhookCommand
                    {
                        Channel = channel,
                        PlatformUserId = messaging.Sender.Id,
                        MessageText = messaging.Message.Text,
                        RecipientId = messaging.Sender.Id,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(messaging.Timestamp)
                    };

                    try
                    {
                        await _webhookHandler.HandleAsync(command, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Meta webhook event");
                        // Continue processing other events
                    }
                }
            }

            // Process changes (Instagram/WhatsApp)
            if (entry.Changes != null)
            {
                foreach (var change in entry.Changes)
                {
                    if (change.Value.Messaging != null)
                    {
                        foreach (var messaging in change.Value.Messaging)
                        {
                            if (messaging.Message?.Text == null)
                            {
                                continue;
                            }

                            var command = new ProcessWebhookCommand
                            {
                                Channel = channel,
                                PlatformUserId = messaging.Sender.Id,
                                MessageText = messaging.Message.Text,
                                RecipientId = messaging.Sender.Id,
                                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(messaging.Timestamp)
                            };

                            try
                            {
                                await _webhookHandler.HandleAsync(command, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing Meta change event");
                            }
                        }
                    }
                }
            }
        }

        return Ok();
    }

    /// <summary>
    /// Handles Gemini Batch API webhook events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK if the verified webhook was accepted.</returns>
    [HttpPost("gemini")]
    public async Task<IActionResult> HandleGeminiWebhook(CancellationToken cancellationToken)
    {
        var requestBody = await ReadRequestBodyAsync();
        var signingSecret = _configuration[GeminiWebhookSigningSecretKey];
        if (string.IsNullOrWhiteSpace(signingSecret))
        {
            _logger.LogError("Gemini webhook signing secret is not configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Gemini webhook is not configured" });
        }

        var webhookId = Request.Headers["webhook-id"].ToString();
        var webhookTimestamp = Request.Headers["webhook-timestamp"].ToString();
        var webhookSignature = Request.Headers["webhook-signature"].ToString();

        if (!StandardWebhookVerifier.TryVerify(
            signingSecret,
            webhookId,
            webhookTimestamp,
            webhookSignature,
            requestBody,
            DateTimeOffset.UtcNow,
            out var failureReason))
        {
            _logger.LogWarning("Invalid Gemini webhook signature: {FailureReason}", failureReason);
            return Unauthorized(new { error = "Invalid signature" });
        }

        GeminiWebhookEvent? webhookEvent;
        try
        {
            webhookEvent = JsonSerializer.Deserialize<GeminiWebhookEvent>(
                requestBody,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid Gemini webhook JSON payload");
            return BadRequest(new { error = "Invalid webhook payload" });
        }

        if (string.IsNullOrWhiteSpace(webhookEvent?.Type))
        {
            return BadRequest(new { error = "Missing webhook event type" });
        }

        if (IsGeminiBatchTerminalEvent(webhookEvent.Type))
        {
            if (await TryMarkGeminiWebhookReceivedAsync(webhookId, cancellationToken))
            {
                await _geminiBatchWebhookQueue.EnqueueAsync(
                    new GeminiBatchWebhookNotification(
                        webhookId,
                        webhookEvent.Type,
                        webhookEvent.Data?.Id),
                    cancellationToken);

                _logger.LogInformation(
                    "Queued Gemini batch webhook {WebhookId} ({EventType}) for {BatchName}",
                    webhookId,
                    webhookEvent.Type,
                    webhookEvent.Data?.Id ?? "unknown batch");
            }
            else
            {
                _logger.LogInformation(
                    "Skipping duplicate Gemini webhook {WebhookId} ({EventType})",
                    webhookId,
                    webhookEvent.Type);
            }
        }

        return Ok(new { status = "received" });
    }

    /// <summary>
    /// Reads the raw request body as a string.
    /// </summary>
    /// <returns>The request body as a string.</returns>
    private async Task<string> ReadRequestBodyAsync()
    {
        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        Request.Body.Position = 0;

        return body;
    }

    private async Task<bool> TryMarkGeminiWebhookReceivedAsync(
        string webhookId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"gemini:webhooks:{webhookId}";
        var existing = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(existing))
        {
            return false;
        }

        await _cache.SetStringAsync(
            cacheKey,
            "1",
            GeminiWebhookDedupeCacheOptions,
            cancellationToken);

        return true;
    }

    private static bool IsGeminiBatchTerminalEvent(string eventType)
        => eventType.Equals("batch.succeeded", StringComparison.OrdinalIgnoreCase)
            || eventType.Equals("batch.failed", StringComparison.OrdinalIgnoreCase)
            || eventType.Equals("batch.cancelled", StringComparison.OrdinalIgnoreCase)
            || eventType.Equals("batch.expired", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps Meta object type to Channel enum.
    /// </summary>
    /// <param name="objectType">The object type from webhook.</param>
    /// <returns>The corresponding channel.</returns>
    private static Channel GetChannelFromObject(string objectType)
    {
        return objectType.ToLowerInvariant() switch
        {
            "page" => Channel.Facebook,
            "instagram" => Channel.Instagram,
            "whatsapp_business_account" => Channel.WhatsApp,
            _ => Channel.Facebook // Default to Facebook
        };
    }
}
