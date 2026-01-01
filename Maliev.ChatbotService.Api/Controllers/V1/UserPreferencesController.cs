using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing user preferences and data.
/// </summary>
[ApiController]
[Route("chatbot/v1/users")]
[Authorize]
public class UserPreferencesController : ControllerBase
{
    private readonly GetUserPreferencesQueryHandler _getPreferencesHandler;
    private readonly DeleteUserDataCommandHandler _deleteDataHandler;
    private readonly ILogger<UserPreferencesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferencesController"/> class.
    /// </summary>
    /// <param name="getPreferencesHandler">The get preferences query handler.</param>
    /// <param name="deleteDataHandler">The delete data command handler.</param>
    /// <param name="logger">The logger.</param>
    public UserPreferencesController(
        GetUserPreferencesQueryHandler getPreferencesHandler,
        DeleteUserDataCommandHandler deleteDataHandler,
        ILogger<UserPreferencesController> logger)
    {
        _getPreferencesHandler = getPreferencesHandler;
        _deleteDataHandler = deleteDataHandler;
        _logger = logger;
    }

    /// <summary>
    /// Gets the authenticated user's preferences with pagination.
    /// </summary>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="pageSize">The page size (default: 20, max: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of user preferences.</returns>
    [HttpGet("me/preferences")]
    [RequirePermission("chatbot.preferences.read")]
    [ProducesResponseType(typeof(PagedPreferencesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedPreferencesResponse>> GetMyPreferences(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user profile ID from claims
            var userProfileIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserProfileId")?.Value;

            if (string.IsNullOrEmpty(userProfileIdClaim) || !Guid.TryParse(userProfileIdClaim, out var userProfileId))
            {
                _logger.LogWarning("User profile ID not found in claims for get preferences request");
                return Unauthorized(new { error = "User not authenticated" });
            }

            // Validate pagination parameters
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1 || pageSize > 100)
            {
                pageSize = 20;
            }

            var query = new GetUserPreferencesQuery
            {
                UserProfileId = userProfileId,
                Page = page,
                PageSize = pageSize
            };

            var result = await _getPreferencesHandler.HandleAsync(query, cancellationToken);

            // Map to PagedPreferencesResponse
            var response = new PagedPreferencesResponse
            {
                Data = result.Preferences.Select(m => new Application.Models.ExtractedPreference
                {
                    Key = m.Key,
                    Value = m.Value,
                    Confidence = m.Confidence,
                    LastUpdated = m.LastUpdatedAt
                }).ToList(),
                Meta = new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user preferences");
            return StatusCode(500, new { error = "Failed to retrieve preferences" });
        }
    }

    /// <summary>
    /// Deletes the authenticated user's data with scope control.
    /// </summary>
    /// <param name="scope">The scope of data to delete (preferences, history, all).</param>
    /// <param name="confirm">Confirmation flag (must be true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation of deletion.</returns>
    [HttpDelete("me/data")]
    [RequirePermission("chatbot.preferences.delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> DeleteMyData(
        [FromQuery] string scope = "preferences",
        [FromQuery] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Require explicit confirmation
            if (!confirm)
            {
                return BadRequest(new { error = "Deletion requires explicit confirmation. Set confirm=true to proceed." });
            }

            // Validate scope
            if (!IsValidScope(scope))
            {
                return BadRequest(new { error = "Invalid scope. Valid values are: preferences, history, all" });
            }

            // Get user profile ID from claims
            var userProfileIdClaim = User.FindFirst("sub")?.Value
                ?? User.FindFirst("UserProfileId")?.Value;

            if (string.IsNullOrEmpty(userProfileIdClaim) || !Guid.TryParse(userProfileIdClaim, out var userProfileId))
            {
                _logger.LogWarning("User profile ID not found in claims for delete data request");
                return Unauthorized(new { error = "User not authenticated" });
            }

            var command = new DeleteUserDataCommand
            {
                UserProfileId = userProfileId,
                Scope = scope,
                Confirm = confirm
            };

            await _deleteDataHandler.HandleAsync(command, cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to delete user data");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user data");
            return StatusCode(500, new { error = "Failed to delete data" });
        }
    }

    private static bool IsValidScope(string scope)
    {
        var validScopes = new[] { "preferences", "history", "all" };
        return validScopes.Contains(scope.ToLowerInvariant());
    }
}
