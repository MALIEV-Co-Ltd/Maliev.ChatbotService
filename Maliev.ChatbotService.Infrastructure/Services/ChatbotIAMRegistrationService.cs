using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service responsible for registering chatbot permissions and roles with the IAM service.
/// </summary>
public class ChatbotIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatbotIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public ChatbotIAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        ILogger<ChatbotIAMRegistrationService> logger)
        : base(httpClientFactory, logger, "chatbot")
    {
    }

    /// <summary>
    /// Defines all permissions required by the Chatbot service.
    /// </summary>
    /// <returns>A collection of permission registrations.</returns>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return new[]
        {
            // Session permissions
            new PermissionRegistration
            {
                PermissionId = "chatbot.sessions.initiate",
                Description = "Initiate a new chatbot conversation session"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.sessions.read",
                Description = "View chatbot session details and history"
            },

            // Message permissions
            new PermissionRegistration
            {
                PermissionId = "chatbot.messages.send",
                Description = "Send messages to the chatbot"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.messages.read",
                Description = "View chatbot message history"
            },

            // User linking and preferences
            new PermissionRegistration
            {
                PermissionId = "chatbot.users.link",
                Description = "Link user identity across platforms"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.preferences.read",
                Description = "View stored user preferences"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.preferences.delete",
                Description = "Delete user preferences and data"
            },

            // System instruction permissions (Admin)
            new PermissionRegistration
            {
                PermissionId = "chatbot.instructions.read",
                Description = "View system instructions"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.instructions.write",
                Description = "Create, update, or delete system instructions"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.instructions.create",
                Description = "Create new system instructions"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.instructions.update",
                Description = "Update existing system instructions"
            },
            new PermissionRegistration
            {
                PermissionId = "chatbot.instructions.delete",
                Description = "Delete system instructions"
            },

            // Internal operations (CRM Agent)
            new PermissionRegistration
            {
                PermissionId = "chatbot.operations.execute",
                Description = "Execute internal operations (CRM queries, order status, etc.)"
            },

            // Metrics and monitoring
            new PermissionRegistration
            {
                PermissionId = "chatbot.metrics.read",
                Description = "View chatbot metrics and analytics"
            }
        };
    }

    /// <summary>
    /// Defines default roles for the Chatbot service.
    /// </summary>
    /// <returns>A collection of role registrations.</returns>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return new[]
        {
            // ChatbotUser role - Standard users interacting with the chatbot
            new RoleRegistration
            {
                RoleId = "chatbot.user",
                Description = "Standard user role for chatbot interactions",
                PermissionIds = new List<string>
                {
                    "chatbot.sessions.initiate",
                    "chatbot.sessions.read",
                    "chatbot.messages.send",
                    "chatbot.messages.read",
                    "chatbot.users.link",
                    "chatbot.preferences.read",
                    "chatbot.preferences.delete"
                },
                IsCustom = false
            },

            // ChatbotAdmin role - Administrators managing chatbot configuration
            new RoleRegistration
            {
                RoleId = "chatbot.admin",
                Description = "Administrator role for full chatbot management and monitoring",
                PermissionIds = new List<string>
                {
                    "chatbot.instructions.read",
                    "chatbot.instructions.write",
                    "chatbot.instructions.create",
                    "chatbot.instructions.update",
                    "chatbot.instructions.delete",
                    "chatbot.metrics.read",
                    "chatbot.sessions.read",
                    "chatbot.messages.read"
                },
                IsCustom = false
            },

            // InternalAgent role - Internal CRM agents using chatbot for operations
            new RoleRegistration
            {
                RoleId = "chatbot.internalagent",
                Description = "Internal agent role for CRM operations and queries",
                PermissionIds = new List<string>
                {
                    "chatbot.operations.execute",
                    "chatbot.sessions.initiate",
                    "chatbot.sessions.read",
                    "chatbot.messages.send",
                    "chatbot.messages.read",
                    "chatbot.preferences.read",
                    "chatbot.metrics.read"
                },
                IsCustom = false
            }
        };
    }
}
