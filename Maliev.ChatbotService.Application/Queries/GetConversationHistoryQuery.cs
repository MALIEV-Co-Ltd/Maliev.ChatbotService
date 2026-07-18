using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Queries;

/// <summary>
/// Query to get conversation sessions for a user.
/// </summary>
public class GetConversationSessionsQuery
{
    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the optional channel filter.
    /// </summary>
    public Channel? Channel { get; set; }

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Query to get messages for one conversation session.
/// </summary>
public class GetConversationMessagesQuery
{
    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }
}
