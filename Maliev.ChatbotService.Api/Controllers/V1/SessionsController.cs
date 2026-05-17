using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Models;
using Maliev.ChatbotService.Application.Queries;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing chatbot sessions.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/sessions")]
public class SessionsController : ControllerBase
{
    private readonly InitiateSessionCommandHandler _handler;
    private readonly LinkIdentityCommandHandler _linkIdentityHandler;
    private readonly GetConversationHistoryQueryHandler _historyHandler;
    private readonly ILogger<SessionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionsController"/> class.
    /// </summary>
    /// <param name="handler">The session initiation handler.</param>
    /// <param name="linkIdentityHandler">The link identity handler.</param>
    /// <param name="historyHandler">The conversation history query handler.</param>
    /// <param name="logger">The logger.</param>
    public SessionsController(
        InitiateSessionCommandHandler handler,
        LinkIdentityCommandHandler linkIdentityHandler,
        GetConversationHistoryQueryHandler historyHandler,
        ILogger<SessionsController> logger)
    {
        _handler = handler;
        _linkIdentityHandler = linkIdentityHandler;
        _historyHandler = historyHandler;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a new chatbot session.
    /// </summary>
    /// <param name="request">The session initiation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created session details.</returns>
    [HttpPost("initiate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> InitiateSession(
        [FromBody] InitiateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (IsIntranetChannel(request.Channel) && User.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("Anonymous caller attempted to initiate an intranet chatbot session");
                return Unauthorized(new { error = "Intranet chatbot sessions require authentication" });
            }

            var authenticatedUserProfileId = TryGetAuthenticatedUserProfileId(out var parsedUserProfileId)
                ? parsedUserProfileId
                : (Guid?)null;

            var command = new InitiateSessionCommand
            {
                Channel = request.Channel,
                ExternalUserId = request.ExternalUserId,
                Language = request.Language,
                AuthenticatedUserProfileId = authenticatedUserProfileId
            };

            var result = await _handler.HandleAsync(command, cancellationToken);

            var response = new SessionResponse
            {
                SessionId = result.SessionId,
                WelcomeMessage = result.WelcomeMessage,
                Language = result.Language == Language.Thai ? "th" : "en",
                ExpiresAt = result.ExpiresAt
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for session initiation");
            return BadRequest(new { error = ex.Message });
        }
    }

    private static bool IsIntranetChannel(string channel)
    {
        return string.Equals(channel, "intranet", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the authenticated user's conversation sessions.
    /// </summary>
    /// <param name="channel">Optional channel filter.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conversation sessions owned by the authenticated user.</returns>
    [HttpGet]
    [RequirePermission(ChatbotPermissions.SessionRead)]
    [ProducesResponseType(typeof(ConversationSessionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ConversationSessionsResponse>> GetMySessions(
        [FromQuery] string? channel = null,
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthenticatedUserProfileId(out var userProfileId))
        {
            _logger.LogWarning("User profile ID not found in claims for session history request");
            return Unauthorized(new { error = "User not authenticated" });
        }

        if (!TryParseChannel(channel, out var parsedChannel))
        {
            return BadRequest(new { error = $"Invalid channel: {channel}" });
        }

        var result = await _historyHandler.GetSessionsAsync(new GetConversationSessionsQuery
        {
            UserProfileId = userProfileId,
            Channel = parsedChannel,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(new ConversationSessionsResponse
        {
            Data = result.Sessions.Select(MapSession).ToList(),
            Meta = new PaginationMeta
            {
                Page = result.Page,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            }
        });
    }

    /// <summary>
    /// Gets ordered messages for one authenticated user-owned conversation session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation messages.</returns>
    [HttpGet("{sessionId:guid}/messages")]
    [RequirePermission(ChatbotPermissions.SessionRead)]
    [ProducesResponseType(typeof(ConversationMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationMessagesResponse>> GetMySessionMessages(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthenticatedUserProfileId(out var userProfileId))
        {
            _logger.LogWarning("User profile ID not found in claims for session messages request");
            return Unauthorized(new { error = "User not authenticated" });
        }

        var result = await _historyHandler.GetMessagesAsync(new GetConversationMessagesQuery
        {
            UserProfileId = userProfileId,
            SessionId = sessionId
        }, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(new ConversationMessagesResponse
        {
            SessionId = result.SessionId,
            UserProfileId = result.UserProfileId,
            Channel = result.Channel,
            StartTime = result.StartTime,
            LastActivityAt = result.LastActivityAt,
            ExpiresAt = result.ExpiresAt,
            Language = result.Language,
            Status = result.Status,
            Messages = result.Messages.Select(MapMessage).ToList()
        });
    }

    /// <summary>
    /// Links an external platform identity to the authenticated user's profile.
    /// </summary>
    /// <param name="request">The link identity request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created identity link details.</returns>
    [HttpPost("link")]
    [RequirePermission(ChatbotPermissions.UsersLink)]
    [ProducesResponseType(typeof(LinkIdentityResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> LinkIdentity(
        [FromBody] LinkIdentityRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get user profile ID from claims
            var userProfileIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserProfileId")?.Value;

            if (string.IsNullOrEmpty(userProfileIdClaim) || !Guid.TryParse(userProfileIdClaim, out var userProfileId))
            {
                _logger.LogWarning("User profile ID not found in claims for link identity request");
                return Unauthorized(new { error = "User not authenticated" });
            }

            var command = new LinkIdentityCommand
            {
                UserProfileId = userProfileId,
                PlatformName = request.PlatformName,
                ExternalUserId = request.ExternalUserId
            };

            var result = await _linkIdentityHandler.HandleAsync(command, cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for identity linking");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to link identity");
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool TryGetAuthenticatedUserProfileId(out Guid userProfileId)
    {
        userProfileId = Guid.Empty;

        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userProfileIdClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst("UserProfileId")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return !string.IsNullOrEmpty(userProfileIdClaim) && Guid.TryParse(userProfileIdClaim, out userProfileId);
    }

    private static bool TryParseChannel(string? channel, out Channel? parsedChannel)
    {
        parsedChannel = null;

        if (string.IsNullOrWhiteSpace(channel))
        {
            return true;
        }

        parsedChannel = channel.Trim().ToLowerInvariant() switch
        {
            "website" => Channel.Website,
            "line" => Channel.Line,
            "facebook" => Channel.Facebook,
            "instagram" => Channel.Instagram,
            "whatsapp" => Channel.WhatsApp,
            "intranet" => Channel.Intranet,
            _ => null
        };

        return parsedChannel.HasValue;
    }

    private static ConversationSessionSummaryResponse MapSession(ConversationSessionHistoryItem session)
    {
        return new ConversationSessionSummaryResponse
        {
            SessionId = session.SessionId,
            UserProfileId = session.UserProfileId,
            Channel = session.Channel,
            StartTime = session.StartTime,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            Language = session.Language,
            Status = session.Status,
            Preview = session.Preview,
            MessageCount = session.MessageCount
        };
    }

    private static ConversationMessageResponse MapMessage(ConversationHistoryMessage message)
    {
        return new ConversationMessageResponse
        {
            MessageId = message.MessageId,
            Role = message.Role,
            Content = message.Content,
            ContentType = message.ContentType,
            CreatedAt = message.CreatedAt
        };
    }
}
