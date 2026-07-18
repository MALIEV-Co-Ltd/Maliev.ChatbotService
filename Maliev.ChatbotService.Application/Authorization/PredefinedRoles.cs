namespace Maliev.ChatbotService.Application.Authorization;

/// <summary>
/// Provides access to predefined roles for the Chatbot Service.
/// </summary>
public static class ChatbotPredefinedRoles
{
    public const string Admin = "roles.chatbot.admin";
    public const string Operator = "roles.chatbot.operator";
    public const string Viewer = "roles.chatbot.viewer";

    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (
            Admin,
            "Chatbot Administrator with full access",
            new[]
            {
                ChatbotPermissions.SessionCreate,
                ChatbotPermissions.SessionRead,
                ChatbotPermissions.SessionEnd,
                ChatbotPermissions.ConversationCreate,
                ChatbotPermissions.ConversationRead,
                ChatbotPermissions.ConversationUpdate,
                ChatbotPermissions.TemplateCreate,
                ChatbotPermissions.TemplateRead,
                ChatbotPermissions.TemplateUpdate,
                ChatbotPermissions.TemplateDelete,
                ChatbotPermissions.FallbackCreate,
                ChatbotPermissions.FallbackRead,
                ChatbotPermissions.FallbackUpdate,
                ChatbotPermissions.FallbackDelete,
                ChatbotPermissions.ResponseCreate,
                ChatbotPermissions.ResponseRead,
                ChatbotPermissions.ResponseUpdate,
                ChatbotPermissions.ResponseDelete,
            }
        ),
        (
            Operator,
            "Chatbot Operator with session and conversation access",
            new[]
            {
                ChatbotPermissions.SessionCreate,
                ChatbotPermissions.SessionRead,
                ChatbotPermissions.SessionEnd,
                ChatbotPermissions.ConversationCreate,
                ChatbotPermissions.ConversationRead,
                ChatbotPermissions.ConversationUpdate,
                ChatbotPermissions.TemplateRead,
                ChatbotPermissions.FallbackRead,
                ChatbotPermissions.ResponseRead,
            }
        ),
        (
            Viewer,
            "Chatbot Viewer with read-only access",
            new[]
            {
                ChatbotPermissions.SessionRead,
                ChatbotPermissions.ConversationRead,
                ChatbotPermissions.TemplateRead,
                ChatbotPermissions.FallbackRead,
                ChatbotPermissions.ResponseRead,
            }
        ),
    };
}
