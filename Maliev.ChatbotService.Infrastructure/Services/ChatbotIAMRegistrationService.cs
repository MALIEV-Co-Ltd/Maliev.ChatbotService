using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Predefined roles for the Chatbot Service.
/// </summary>
public static class ChatbotPredefinedRoles
{
    /// <summary>Standard user role for chatbot interactions.</summary>
    public const string User = "roles.chatbot.user";
    /// <summary>Administrator role for full chatbot management.</summary>
    public const string Admin = "roles.chatbot.admin";
    /// <summary>Internal agent role for CRM operations.</summary>
    public const string InternalAgent = "roles.chatbot.internalagent";

    /// <summary>
    /// Collection of all predefined roles for the Chatbot Service.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (User, "Standard user role for chatbot interactions", new[]
        {
            ChatbotPermissions.SessionsInitiate,
            ChatbotPermissions.SessionsRead,
            ChatbotPermissions.MessagesSend,
            ChatbotPermissions.MessagesRead,
            ChatbotPermissions.UsersLink,
            ChatbotPermissions.PreferencesRead,
            ChatbotPermissions.PreferencesDelete
        }),
        (Admin, "Administrator role for full chatbot management and monitoring", new[]
        {
            ChatbotPermissions.InstructionsRead,
            ChatbotPermissions.InstructionsWrite,
            ChatbotPermissions.InstructionsCreate,
            ChatbotPermissions.InstructionsUpdate,
            ChatbotPermissions.InstructionsDelete,
            ChatbotPermissions.MetricsRead,
            ChatbotPermissions.SessionsRead,
            ChatbotPermissions.MessagesRead
        }),
        (InternalAgent, "Internal agent role for CRM operations and queries", new[]
        {
            ChatbotPermissions.OperationsExecute,
            ChatbotPermissions.SessionsInitiate,
            ChatbotPermissions.SessionsRead,
            ChatbotPermissions.MessagesSend,
            ChatbotPermissions.MessagesRead,
            ChatbotPermissions.PreferencesRead,
            ChatbotPermissions.MetricsRead
        })
    };
}

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
        return ChatbotPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
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
        return ChatbotPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}


