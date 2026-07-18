namespace Maliev.ChatbotService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Chatbot Service.
/// </summary>
public static class ChatbotPermissions
{
    public const string SessionCreate = "chatbot.sessions.create";
    public const string SessionRead = "chatbot.sessions.read";
    public const string SessionEnd = "chatbot.sessions.end";

    public const string ConversationCreate = "chatbot.conversations.create";
    public const string ConversationRead = "chatbot.conversations.read";
    public const string ConversationUpdate = "chatbot.conversations.update";

    public const string TemplateCreate = "chatbot.templates.create";
    public const string TemplateRead = "chatbot.templates.read";
    public const string TemplateUpdate = "chatbot.templates.update";
    public const string TemplateDelete = "chatbot.templates.delete";

    public const string FallbackCreate = "chatbot.fallbacks.create";
    public const string FallbackRead = "chatbot.fallbacks.read";
    public const string FallbackUpdate = "chatbot.fallbacks.update";
    public const string FallbackDelete = "chatbot.fallbacks.delete";

    public const string ResponseCreate = "chatbot.responses.create";
    public const string ResponseRead = "chatbot.responses.read";
    public const string ResponseUpdate = "chatbot.responses.update";
    public const string ResponseDelete = "chatbot.responses.delete";

    public const string PreferencesRead = "chatbot.preferences.read";
    public const string PreferencesDelete = "chatbot.preferences.delete";

    public const string InstructionsRead = "chatbot.instructions.read";
    public const string InstructionsWrite = "chatbot.instructions.write";

    public const string UsersLink = "chatbot.users.link";

    public const string KnowledgeRead = "chatbot.knowledge.read";
    public const string KnowledgeWrite = "chatbot.knowledge.write";
    public const string ExtractionsRun = "chatbot.extractions.run";

    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { SessionCreate, "Create chatbot sessions" },
        { SessionRead, "Read chatbot sessions" },
        { SessionEnd, "End chatbot sessions" },
        { ConversationCreate, "Create conversations" },
        { ConversationRead, "Read conversations" },
        { ConversationUpdate, "Update conversations" },
        { TemplateCreate, "Create chatbot templates" },
        { TemplateRead, "Read chatbot templates" },
        { TemplateUpdate, "Update chatbot templates" },
        { TemplateDelete, "Delete chatbot templates" },
        { FallbackCreate, "Create fallback handlers" },
        { FallbackRead, "Read fallback handlers" },
        { FallbackUpdate, "Update fallback handlers" },
        { FallbackDelete, "Delete fallback handlers" },
        { ResponseCreate, "Create chatbot responses" },
        { ResponseRead, "Read chatbot responses" },
        { ResponseUpdate, "Update chatbot responses" },
        { ResponseDelete, "Delete chatbot responses" },
        { PreferencesRead, "Read user preferences" },
        { PreferencesDelete, "Delete user preferences" },
        { InstructionsRead, "Read system instructions" },
        { InstructionsWrite, "Write system instructions" },
        { UsersLink, "Link chatbot users" },
        { KnowledgeRead, "Read knowledge base" },
        { KnowledgeWrite, "Write knowledge base" },
        { ExtractionsRun, "Run AI extraction workflows" },
    };

    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
