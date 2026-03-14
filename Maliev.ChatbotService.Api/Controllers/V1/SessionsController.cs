using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing chatbot sessions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("chatbot/v{version:apiVersion}/sessions")]
public class SessionsController : ControllerBase
{
    private readonly InitiateSessionCommandHandler _handler;
    private readonly LinkIdentityCommandHandler _linkIdentityHandler;
    private readonly ILogger<SessionsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionsController"/> class.
    /// </summary>
    /// <param name="handler">The session initiation handler.</param>
    /// <param name="linkIdentityHandler">The link identity handler.</param>
    /// <param name="logger">The logger.</param>
    public SessionsController(
        InitiateSessionCommandHandler handler,
        LinkIdentityCommandHandler linkIdentityHandler,
        ILogger<SessionsController> logger)
    {
        _handler = handler;
        _linkIdentityHandler = linkIdentityHandler;
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
    public async Task<ActionResult<SessionResponse>> InitiateSession(
        [FromBody] InitiateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get authenticated user profile ID from claims if available
            Guid? authenticatedUserProfileId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userProfileIdClaim = User.FindFirst("sub")?.Value
                    ?? User.FindFirst("UserProfileId")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(userProfileIdClaim) && Guid.TryParse(userProfileIdClaim, out var parsedId))
                {
                    authenticatedUserProfileId = parsedId;
                }
            }

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

    /// <summary>
    /// Links an external platform identity to the authenticated user's profile.
    /// </summary>
    /// <param name="request">The link identity request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created identity link details.</returns>
    [HttpPost("link")]
    [RequirePermission("chatbot.users.link")]
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
}
