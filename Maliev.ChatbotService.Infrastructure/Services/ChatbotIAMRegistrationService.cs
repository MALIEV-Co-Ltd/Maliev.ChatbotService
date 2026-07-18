using Maliev.Aspire.ServiceDefaults.IAM;
using ApplicationChatbotPermissions = Maliev.ChatbotService.Application.Authorization.ChatbotPermissions;
using ApplicationChatbotPredefinedRoles = Maliev.ChatbotService.Application.Authorization.ChatbotPredefinedRoles;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service responsible for registering chatbot permissions and roles with the IAM service.
/// </summary>
public class ChatbotIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatbotIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger.</param>
    public ChatbotIAMRegistrationService(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<ChatbotIAMRegistrationService> logger)
        : base(configuration, logger, "chatbot")
    {
    }

    /// <summary>
    /// Defines all permissions required by the Chatbot service.
    /// </summary>
    /// <returns>A collection of permission registrations.</returns>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return ApplicationChatbotPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <summary>
    /// Defines default roles for the Chatbot service.
    /// </summary>
    /// <returns>A collection of role registrations.</returns>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return ApplicationChatbotPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}


