namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Constants for Chatbot Service permissions.
/// Follows GCP-style naming: {service}.{resource}.{action}
/// </summary>
public static class ChatbotPermissions
{
    // Session permissions
    /// <summary>Permission to initiate chatbot sessions.</summary>
    public const string SessionsInitiate = "chatbot.sessions.initiate";
    /// <summary>Permission to read chatbot sessions.</summary>
    public const string SessionsRead = "chatbot.sessions.read";

    // Message permissions
    /// <summary>Permission to send chatbot messages.</summary>
    public const string MessagesSend = "chatbot.messages.send";
    /// <summary>Permission to read chatbot messages.</summary>
    public const string MessagesRead = "chatbot.messages.read";

    // User linking and preferences
    /// <summary>Permission to link users.</summary>
    public const string UsersLink = "chatbot.users.link";
    /// <summary>Permission to read user preferences.</summary>
    public const string PreferencesRead = "chatbot.preferences.read";
    /// <summary>Permission to delete user preferences.</summary>
    public const string PreferencesDelete = "chatbot.preferences.delete";

    // System instruction permissions (Admin)
    /// <summary>Permission to read system instructions.</summary>
    public const string InstructionsRead = "chatbot.instructions.read";
    /// <summary>Permission to write system instructions.</summary>
    public const string InstructionsWrite = "chatbot.instructions.write";
    /// <summary>Permission to create system instructions.</summary>
    public const string InstructionsCreate = "chatbot.instructions.create";
    /// <summary>Permission to update system instructions.</summary>
    public const string InstructionsUpdate = "chatbot.instructions.update";
    /// <summary>Permission to delete system instructions.</summary>
    public const string InstructionsDelete = "chatbot.instructions.delete";

    // Internal operations (CRM Agent)
    /// <summary>Permission to execute internal operations.</summary>
    public const string OperationsExecute = "chatbot.operations.execute";

    // Metrics and monitoring
    /// <summary>Permission to read chatbot metrics.</summary>
    public const string MetricsRead = "chatbot.metrics.read";

    /// <summary>
    /// Collection of all defined chatbot permissions with descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { SessionsInitiate, "Initiate a new chatbot conversation session" },
        { SessionsRead, "View chatbot session details and history" },
        { MessagesSend, "Send messages to the chatbot" },
        { MessagesRead, "View chatbot message history" },
        { UsersLink, "Link user identity across platforms" },
        { PreferencesRead, "View stored user preferences" },
        { PreferencesDelete, "Delete user preferences and data" },
        { InstructionsRead, "View system instructions" },
        { InstructionsWrite, "Create, update, or delete system instructions" },
        { InstructionsCreate, "Create new system instructions" },
        { InstructionsUpdate, "Update existing system instructions" },
        { InstructionsDelete, "Delete system instructions" },
        { OperationsExecute, "Execute internal operations (CRM queries, order status, etc.)" },
        { MetricsRead, "View chatbot metrics and analytics" }
    };
}
