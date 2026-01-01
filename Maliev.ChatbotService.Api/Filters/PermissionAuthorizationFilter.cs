using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Maliev.ChatbotService.Api.Filters;

/// <summary>
/// Authorization filter that checks IAM permissions for API endpoints.
/// </summary>
public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IIAMServiceClient _iamServiceClient;
    private readonly ILogger<PermissionAuthorizationFilter> _logger;
    private readonly string _requiredPermission;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionAuthorizationFilter"/> class.
    /// </summary>
    /// <param name="iamServiceClient">The IAM service client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="requiredPermission">The required permission for the endpoint.</param>
    public PermissionAuthorizationFilter(
        IIAMServiceClient iamServiceClient,
        ILogger<PermissionAuthorizationFilter> logger,
        string requiredPermission)
    {
        _iamServiceClient = iamServiceClient;
        _logger = logger;
        _requiredPermission = requiredPermission;
    }

    /// <inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Get user ID from claims
        var userIdClaim = context.HttpContext.User.FindFirst("sub") ?? context.HttpContext.User.FindFirst("user_id");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Authorization failed: No valid user ID claim found");
            context.Result = new ForbidResult();
            return;
        }

        try
        {
            // Check permission via IAM service
            var hasPermission = await _iamServiceClient.HasPermissionAsync(userId.ToString(), _requiredPermission);
            if (!hasPermission)
            {
                _logger.LogWarning("User {UserId} does not have permission {Permission}", userId, _requiredPermission);
                context.Result = new ForbidResult();
                return;
            }

            _logger.LogDebug("User {UserId} authorized with permission {Permission}", userId, _requiredPermission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for user {UserId}", _requiredPermission, userId);
            context.Result = new StatusCodeResult(503);
        }
    }
}

/// <summary>
/// Attribute to require specific permissions for controller actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// Gets the required permission.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class.
    /// </summary>
    /// <param name="permission">The required permission.</param>
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
    }
}
