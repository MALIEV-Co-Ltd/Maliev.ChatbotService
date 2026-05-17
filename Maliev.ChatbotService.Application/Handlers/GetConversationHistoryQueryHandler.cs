using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Maliev.ChatbotService.Application.Queries;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for querying conversation history owned by a user profile.
/// </summary>
public class GetConversationHistoryQueryHandler
{
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<GetConversationHistoryQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetConversationHistoryQueryHandler"/> class.
    /// </summary>
    /// <param name="sessionRepository">The session repository.</param>
    /// <param name="messageRepository">The message repository.</param>
    /// <param name="logger">The logger.</param>
    public GetConversationHistoryQueryHandler(
        IConversationSessionRepository sessionRepository,
        IMessageRepository messageRepository,
        ILogger<GetConversationHistoryQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets conversation sessions for a user profile.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The paged conversation session result.</returns>
    public async Task<GetConversationSessionsResult> GetSessionsAsync(
        GetConversationSessionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        _logger.LogInformation(
            "Fetching conversation sessions for user {UserProfileId}, channel {Channel}, page {Page}, size {PageSize}",
            query.UserProfileId,
            query.Channel,
            page,
            pageSize);

        var (sessions, totalCount) = await _sessionRepository.GetByUserIdAndChannelAsync(
            query.UserProfileId,
            query.Channel,
            page,
            pageSize,
            cancellationToken);

        var items = new List<ConversationSessionHistoryItem>(sessions.Count);
        foreach (var session in sessions)
        {
            var messages = await _messageRepository.GetMessagesBySessionIdAsync(session.Id, cancellationToken);
            items.Add(MapSession(session, messages));
        }

        return new GetConversationSessionsResult
        {
            Sessions = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Gets messages for one user-owned conversation session.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation messages, or null when the session is not owned by the user.</returns>
    public async Task<GetConversationMessagesResult?> GetMessagesAsync(
        GetConversationMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken);
        if (session is null || session.UserProfileId != query.UserProfileId)
        {
            return null;
        }

        var messages = await _messageRepository.GetMessagesBySessionIdAsync(session.Id, cancellationToken);

        return new GetConversationMessagesResult
        {
            SessionId = session.Id,
            UserProfileId = session.UserProfileId,
            Channel = ToWireValue(session.Channel),
            StartTime = session.StartTime,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            Language = ToWireValue(session.Language),
            Status = ToWireValue(session.Status),
            Messages = messages.Select(MapMessage).ToList()
        };
    }

    private static ConversationSessionHistoryItem MapSession(ConversationSession session, List<Message> messages)
    {
        var preview = messages
            .FirstOrDefault(message => message.Role == MessageRole.User && !string.IsNullOrWhiteSpace(message.Content))
            ?.Content;

        return new ConversationSessionHistoryItem
        {
            SessionId = session.Id,
            UserProfileId = session.UserProfileId,
            Channel = ToWireValue(session.Channel),
            StartTime = session.StartTime,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            Language = ToWireValue(session.Language),
            Status = ToWireValue(session.Status),
            Preview = TrimPreview(preview),
            MessageCount = messages.Count
        };
    }

    private static ConversationHistoryMessage MapMessage(Message message)
    {
        return new ConversationHistoryMessage
        {
            MessageId = message.Id,
            Role = ToWireValue(message.Role),
            Content = message.Content,
            ContentType = ToWireValue(message.ContentType),
            CreatedAt = message.CreatedAt
        };
    }

    private static string ToWireValue(Channel channel) => channel.ToString().ToLowerInvariant();

    private static string ToWireValue(Language language) => language == Language.Thai ? "th" : "en";

    private static string ToWireValue(SessionStatus status) => status.ToString().ToLowerInvariant();

    private static string ToWireValue(MessageRole role) => role.ToString().ToLowerInvariant();

    private static string ToWireValue(ContentType contentType) => contentType.ToString().ToLowerInvariant();

    private static string? TrimPreview(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 120 ? $"{trimmed[..117]}..." : trimmed;
    }
}
