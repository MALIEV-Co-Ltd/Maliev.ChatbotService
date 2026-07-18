using Asp.Versioning;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Models;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing chatbot messages.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/messages")]
[EnableRateLimiting(ChatbotRateLimiterPolicies.MessagesPerIp)]
public class MessagesController : ControllerBase
{
    private readonly SendMessageCommandHandler _handler;
    private readonly AgentChatHandler _agentHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<MessagesController> _logger;

    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Initializes a new instance of the MessagesController class.
    /// </summary>
    /// <param name="handler">Command handler for sending messages.</param>
    /// <param name="agentHandler">Agent chat handler.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="hostEnvironment">Host environment.</param>
    /// <param name="logger">Logger instance.</param>
    public MessagesController(
        SendMessageCommandHandler handler,
        AgentChatHandler agentHandler,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<MessagesController> logger)
    {
        _handler = handler;
        _agentHandler = agentHandler;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message in a chatbot session.
    /// </summary>
    /// <param name="request">The message request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI assistant's response.</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = BuildSendMessageCommand(request, cancellationToken);
            if (command == null)
            {
                return BadRequest(new { error = "CallbackUrl must be a same-origin HTTPS URL or same-origin absolute path." });
            }

            var result = await _handler.HandleAsync(command, cancellationToken);

            var response = MapMessageResponse(result);

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit exceeded"))
        {
            _logger.LogWarning(ex, "Rate limit exceeded for session {SessionId}", request.SessionId);

            // Calculate retry-after time (1 hour from now)
            var retryAfterSeconds = 3600;
            Response.Headers.Append("Retry-After", retryAfterSeconds.ToString());
            Response.Headers.Append("X-RateLimit-Limit", "100");
            Response.Headers.Append("X-RateLimit-Remaining", "0");

            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for session {SessionId}", request.SessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Streams a message response in a chatbot session as newline-delimited JSON events.
    /// </summary>
    /// <param name="request">The message request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A newline-delimited JSON response stream.</returns>
    [HttpPost("stream")]
    [AllowAnonymous]
    [Produces("application/x-ndjson")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> StreamMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = BuildSendMessageCommand(request, cancellationToken);
        if (command == null)
        {
            return BadRequest(new { error = "CallbackUrl must be a same-origin HTTPS URL or same-origin absolute path." });
        }

        Response.ContentType = "application/x-ndjson; charset=utf-8";
        command.TextDeltaCallback = delta => WriteStreamEventAsync(new MessageStreamEvent
        {
            Type = "delta",
            Delta = delta
        }, cancellationToken);

        command.ThoughtDeltaCallback = thought => WriteStreamEventAsync(new MessageStreamEvent
        {
            Type = "thought",
            Thought = thought
        }, cancellationToken);

        try
        {
            await WriteStreamEventAsync(new MessageStreamEvent { Type = "started" }, cancellationToken);
            var result = await _handler.HandleAsync(command, cancellationToken);
            await WriteStreamEventAsync(new MessageStreamEvent
            {
                Type = "final",
                Message = MapMessageResponse(result)
            }, cancellationToken);

            return new EmptyResult();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit exceeded"))
        {
            _logger.LogWarning(ex, "Rate limit exceeded for session {SessionId}", request.SessionId);
            await WriteStreamEventAsync(new MessageStreamEvent
            {
                Type = "error",
                Error = ex.Message
            }, cancellationToken);

            return new EmptyResult();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for session {SessionId}", request.SessionId);
            await WriteStreamEventAsync(new MessageStreamEvent
            {
                Type = "error",
                Error = ex.Message
            }, cancellationToken);

            return new EmptyResult();
        }
    }

    private SendMessageCommand? BuildSendMessageCommand(SendMessageRequest request, CancellationToken cancellationToken)
    {
        var command = new SendMessageCommand
        {
            SessionId = request.SessionId,
            Content = request.Content,
            Language = request.Language,
            ModelName = request.ModelName,
            Attachments = request.Attachments?.Select(a => new AttachmentDto
            {
                ContentType = a.Type?.ToLowerInvariant() switch
                {
                    "image" => ContentType.Image,
                    "pdf" => ContentType.PDF,
                    "video" => ContentType.Video,
                    "audio" => ContentType.Audio,
                    "document" or "text" or "file" => ContentType.Text,
                    _ => ContentType.Image
                },
                Data = a.Url,
                MimeType = ResolveAttachmentMimeType(a),
                SizeBytes = a.SizeBytes ?? 0
            }).ToList(),
            ResponseMimeType = request.ResponseMimeType,
            ResponseSchema = request.ResponseSchema,
            QuoteAgentContextToken = request.QuoteAgentContextToken
        };

        // Forward the authenticated user's Bearer token for downstream tool calls
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            command.UserToken = authHeader["Bearer ".Length..];

        // Build callback for streaming thinking steps
        Func<ThinkingStep, Task>? thinkingCallback = null;
        if (!string.IsNullOrEmpty(request.CallbackUrl))
        {
            if (!TryBuildSafeCallbackUri(request.CallbackUrl, out var callbackUri))
            {
                _logger.LogWarning("Rejected unsafe thinking-step callback URL for session {SessionId}", request.SessionId);
                return null;
            }

            if (!callbackUri.IsAbsoluteUri)
            {
                _logger.LogWarning("Rejected non-absolute thinking-step callback URL for session {SessionId}", request.SessionId);
                return null;
            }

            var client = _httpClientFactory.CreateClient("ThinkingCallback");
            thinkingCallback = async step =>
            {
                try
                {
                    await client.PostAsJsonAsync(callbackUri, step, SnakeCaseOptions, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send thinking step callback for session {SessionId}", request.SessionId);
                }
            };
        }

        command.ThinkingStepCallback = thinkingCallback;
        return command;
    }

    private static string ResolveAttachmentMimeType(Attachment attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.MimeType))
        {
            return attachment.MimeType.Trim();
        }

        var extension = GetAttachmentExtension(attachment.Url);
        if (!string.IsNullOrWhiteSpace(extension) &&
            ExtensionMimeTypes.TryGetValue(extension, out var inferredMimeType))
        {
            return inferredMimeType;
        }

        return attachment.Type?.ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            _ => string.Empty
        };
    }

    private static string GetAttachmentExtension(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var candidate = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath
            : url.Split('?', '#')[0];

        return Path.GetExtension(candidate).ToLowerInvariant();
    }

    private static readonly IReadOnlyDictionary<string, string> ExtensionMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html",
            [".htm"] = "text/html",
            [".css"] = "text/css",
            [".txt"] = "text/plain",
            [".xml"] = "text/xml",
            [".csv"] = "text/csv",
            [".rtf"] = "text/rtf",
            [".md"] = "text/markdown",
            [".markdown"] = "text/markdown",
            [".js"] = "text/javascript",
            [".json"] = "application/json",
            [".pdf"] = "application/pdf",
            [".bmp"] = "image/bmp",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".mp4"] = "video/mp4",
            [".mpeg"] = "video/mpeg",
            [".mpg"] = "video/mpg",
            [".mov"] = "video/quicktime",
            [".avi"] = "video/avi",
            [".flv"] = "video/x-flv",
            [".webm"] = "video/webm",
            [".wmv"] = "video/wmv",
            [".3gp"] = "video/3gpp"
        };

    private static MessageResponse MapMessageResponse(SendMessageResult result)
    {
        var thinkingSteps = result.ThinkingSteps.Select(ts => new ThinkingStepResponse
        {
            StepNumber = ts.StepNumber,
            Type = ts.Type,
            Title = ts.Title,
            Detail = ts.Detail,
            Timestamp = ts.Timestamp,
            DurationMs = ts.DurationMs
        }).ToList();

        if (!string.IsNullOrEmpty(result.ThoughtContent))
        {
            var maxStep = thinkingSteps.Count > 0 ? thinkingSteps.Max(s => s.StepNumber) : 0;
            thinkingSteps.Add(new ThinkingStepResponse
            {
                StepNumber = maxStep + 1,
                Type = "reasoning",
                Title = "Model reasoning",
                Detail = result.ThoughtContent,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return new MessageResponse
        {
            MessageId = result.MessageId,
            Content = result.Content,
            Role = result.Role == MessageRole.Assistant ? "assistant" : "user",
            Language = result.Language == Language.Thai ? "th" : "en",
            SuggestedActions = result.SuggestedActions.Select(sa => new SuggestedAction
            {
                Text = sa.Text,
                Label = sa.Text,
                Action = sa.Type,
                Data = sa.Data
            }).ToList(),
            CreatedAt = result.CreatedAt,
            ThinkingSteps = thinkingSteps,
            GroundingProvenance = result.GroundingProvenance is null
                ? null
                : new GroundingProvenanceResponse
                {
                    Purpose = result.GroundingProvenance.Purpose,
                    Provider = result.GroundingProvenance.Provider,
                    Status = result.GroundingProvenance.Status,
                    Queries = result.GroundingProvenance.Queries.ToList(),
                    ErrorCode = result.GroundingProvenance.ErrorCode,
                    Sources = result.GroundingProvenance.Sources
                        .Select(source => new GroundingSourceResponse
                        {
                            Title = source.Title,
                            Url = source.Url,
                            Domain = source.Domain
                        })
                        .ToList()
                },
            UsageSnapshot = result.UsageSnapshot is null
                ? null
                : new UsageSnapshotResponse
                {
                    IsEnabled = result.UsageSnapshot.IsEnabled,
                    UsedTokens = result.UsageSnapshot.UsedTokens,
                    DailyTokenBudget = result.UsageSnapshot.DailyTokenBudget,
                    RemainingTokens = result.UsageSnapshot.RemainingTokens,
                    UsedRatio = result.UsageSnapshot.UsedRatio,
                    UsedCostMicroUsd = result.UsageSnapshot.UsedCostMicroUsd,
                    DailyCostBudgetMicroUsd = result.UsageSnapshot.DailyCostBudgetMicroUsd,
                    RemainingCostMicroUsd = result.UsageSnapshot.RemainingCostMicroUsd,
                    CostUsedRatio = result.UsageSnapshot.CostUsedRatio,
                    IsTokenExceeded = result.UsageSnapshot.IsTokenExceeded,
                    IsCostExceeded = result.UsageSnapshot.IsCostExceeded,
                    IsExceeded = result.UsageSnapshot.IsExceeded
                }
        };
    }

    private async Task WriteStreamEventAsync(MessageStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        await Response.WriteAsync(JsonSerializer.Serialize(streamEvent, SnakeCaseOptions), cancellationToken);
        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private bool TryBuildSafeCallbackUri(string callbackUrl, out Uri callbackUri)
    {
        callbackUri = null!;

        if (Uri.TryCreate(callbackUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (!IsSafeAbsoluteCallbackUri(absoluteUri))
            {
                return false;
            }

            callbackUri = absoluteUri;
            return true;
        }

        if (Uri.TryCreate(callbackUrl, UriKind.Relative, out var relativeUri))
        {
            if (!callbackUrl.StartsWith("/", StringComparison.Ordinal) ||
                callbackUrl.StartsWith("//", StringComparison.Ordinal) ||
                !Request.Host.HasValue)
            {
                return false;
            }

            var resolvedUri = new Uri(new Uri($"{Request.Scheme}://{Request.Host}"), relativeUri);
            if (!resolvedUri.IsAbsoluteUri || !IsSafeAbsoluteCallbackUri(resolvedUri))
            {
                return false;
            }

            callbackUri = resolvedUri;
            return true;
        }

        return false;
    }

    private bool IsSafeAbsoluteCallbackUri(Uri absoluteUri)
    {
        if (!string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !IsNonProductionLoopbackCallback(absoluteUri))
        {
            return false;
        }

        if (!string.Equals(absoluteUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return IsAllowedCallbackOrigin(absoluteUri) || IsNonProductionLoopbackCallback(absoluteUri);
        }

        return !Request.Host.Port.HasValue ||
            absoluteUri.Port == Request.Host.Port.Value ||
            IsAllowedCallbackOrigin(absoluteUri) ||
            IsNonProductionLoopbackCallback(absoluteUri);
    }

    private bool IsAllowedCallbackOrigin(Uri callbackUri)
    {
        var configuredOrigins = _configuration
            .GetSection("Chatbot:AllowedThinkingCallbackOrigins")
            .Get<string[]>() ?? [];

        return configuredOrigins
            .SelectMany(origin => origin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(origin => Uri.TryCreate(origin, UriKind.Absolute, out var allowed) &&
                string.Equals(allowed.Scheme, callbackUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(allowed.Host, callbackUri.Host, StringComparison.OrdinalIgnoreCase) &&
                allowed.Port == callbackUri.Port);
    }

    private bool IsNonProductionLoopbackCallback(Uri callbackUri)
    {
        if (_hostEnvironment.IsProduction())
        {
            return false;
        }

        return callbackUri.IsLoopback &&
            (string.Equals(callbackUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(callbackUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));
    }
}
