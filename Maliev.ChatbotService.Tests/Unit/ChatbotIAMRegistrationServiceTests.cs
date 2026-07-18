using Maliev.ChatbotService.Infrastructure.Services;
using ApplicationChatbotPermissions = Maliev.ChatbotService.Application.Authorization.ChatbotPermissions;
using ApplicationChatbotPredefinedRoles = Maliev.ChatbotService.Application.Authorization.ChatbotPredefinedRoles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ChatbotIAMRegistrationServiceTests
{
    [Fact]
    public void GetPermissionsForPublish_RegistersEndpointAuthorizationPermissions()
    {
        var service = CreateService();

        var permissions = service.GetPermissionsForPublish()
            .Select(permission => permission.PermissionId)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(ApplicationChatbotPermissions.SessionRead, permissions);
        Assert.Contains(ApplicationChatbotPermissions.ConversationRead, permissions);
        Assert.Contains(ApplicationChatbotPermissions.ConversationUpdate, permissions);
        Assert.Contains(ApplicationChatbotPermissions.KnowledgeRead, permissions);
        Assert.Contains(ApplicationChatbotPermissions.ExtractionsRun, permissions);
        Assert.DoesNotContain("chatbot.sessions.initiate", permissions);
        Assert.DoesNotContain("chatbot.messages.send", permissions);
    }

    [Fact]
    public void GetRolesForPublish_OperatorRoleCanUpdateConversationsForMakeStudioEditRollback()
    {
        var service = CreateService();

        var roles = service.GetRolesForPublish().ToList();
        var operatorRole = Assert.Single(roles, role => role.RoleId == ApplicationChatbotPredefinedRoles.Operator);

        Assert.Contains(ApplicationChatbotPermissions.SessionRead, operatorRole.PermissionIds);
        Assert.Contains(ApplicationChatbotPermissions.ConversationRead, operatorRole.PermissionIds);
        Assert.Contains(ApplicationChatbotPermissions.ConversationUpdate, operatorRole.PermissionIds);
    }

    private static ChatbotIAMRegistrationService CreateService()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ChatbotIAMRegistrationService(
            configuration,
            NullLogger<ChatbotIAMRegistrationService>.Instance);
    }
}
