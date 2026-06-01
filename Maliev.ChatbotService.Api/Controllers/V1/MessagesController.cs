using Asp.Versioning;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
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
public class MessagesController : ControllerBase
{
    private readonly SendMessageCommandHandler _handler;
    private readonly AgentChatHandler _agentHandler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<MessagesController> _logger;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
            var command = new SendMessageCommand
            {
                SessionId = request.SessionId,
                Content = request.Content,
                Language = request.Language,
                Attachments = request.Attachments?.Select(a => new AttachmentDto
                {
                    ContentType = a.Type?.ToLowerInvariant() switch
                    {
                        "image" => ContentType.Image,
                        "pdf" => ContentType.PDF,
                        "video" => ContentType.Video,
                        "audio" => ContentType.Audio,
                        _ => ContentType.Image
                    },
                    Data = a.Url,
                    MimeType = a.MimeType ?? string.Empty,
                    SizeBytes = a.SizeBytes ?? 0
                }).ToList(),
                ResponseMimeType = request.ResponseMimeType,
                ResponseSchema = request.ResponseSchema
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
                    return BadRequest(new { error = "CallbackUrl must be a same-origin HTTPS URL or same-origin absolute path." });
                }

                var client = _httpClientFactory.CreateClient("ThinkingCallback");
                thinkingCallback = async step =>
                {
                    try
                    {
                        await client.PostAsJsonAsync(callbackUri, step, CamelCaseOptions, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send thinking step callback for session {SessionId}", request.SessionId);
                    }
                };
            }

            command.ThinkingStepCallback = thinkingCallback;

            var result = await _handler.HandleAsync(command, cancellationToken);

            var response = new MessageResponse
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
                ThinkingSteps = result.ThinkingSteps.Select(ts => new ThinkingStepResponse
                {
                    StepNumber = ts.StepNumber,
                    Type = ts.Type,
                    Title = ts.Title,
                    Detail = ts.Detail,
                    Timestamp = ts.Timestamp,
                    DurationMs = ts.DurationMs
                }).ToList()
            };

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

    private bool TryBuildSafeCallbackUri(string callbackUrl, out Uri callbackUri)
    {
        callbackUri = null!;

        if (Uri.TryCreate(callbackUrl, UriKind.Relative, out var relativeUri))
        {
            if (!callbackUrl.StartsWith("/", StringComparison.Ordinal) ||
                callbackUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            callbackUri = new Uri(new Uri($"{Request.Scheme}://{Request.Host}"), relativeUri);
            return true;
        }

        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var absoluteUri))
        {
            return false;
        }

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
