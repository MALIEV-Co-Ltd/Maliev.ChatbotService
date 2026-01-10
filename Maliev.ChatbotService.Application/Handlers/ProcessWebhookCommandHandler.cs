using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for processing webhook events from messaging platforms.
/// </summary>
public class ProcessWebhookCommandHandler
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IIdentityLinkRepository _identityLinkRepository;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly SendMessageCommandHandler _sendMessageHandler;
    private readonly ILineClient _lineClient;
    private readonly IMetaClient _metaClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ProcessWebhookCommandHandler> _logger;

    private const string BufferKeyPrefix = "chatbot:buffer:";
    private const string TimerKeyPrefix = "chatbot:timer:";
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessWebhookCommandHandler"/> class.
    /// </summary>
    /// <param name="userProfileRepository">The user profile repository.</param>
    /// <param name="identityLinkRepository">The identity link repository.</param>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="sendMessageHandler">The send message handler.</param>
    /// <param name="lineClient">The LINE client.</param>
    /// <param name="metaClient">The Meta client.</param>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger.</param>
    public ProcessWebhookCommandHandler(
        IUserProfileRepository userProfileRepository,
        IIdentityLinkRepository identityLinkRepository,
        IConversationSessionRepository sessionRepository,
        SendMessageCommandHandler sendMessageHandler,
        ILineClient lineClient,
        IMetaClient metaClient,
        IConnectionMultiplexer redis,
        ILogger<ProcessWebhookCommandHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _identityLinkRepository = identityLinkRepository;
        _sessionRepository = sessionRepository;
        _sendMessageHandler = sendMessageHandler;
        _lineClient = lineClient;
        _metaClient = metaClient;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Handles the ProcessWebhookCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleAsync(ProcessWebhookCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing webhook from {Channel} for user {UserId}", command.Channel, command.PlatformUserId);

        // Get or create UserProfile from IdentityLink
        var identityLink = await _identityLinkRepository.GetByPlatformIdAsync(
            GetPlatformName(command.Channel),
            command.PlatformUserId,
            cancellationToken);

        UserProfile userProfile;
        if (identityLink == null)
        {
            // Create new user profile
            userProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                LastActiveAt = DateTimeOffset.UtcNow,
                Role = UserRole.Customer
            };

            await _userProfileRepository.CreateAsync(userProfile, cancellationToken);

            // Create identity link
            identityLink = new IdentityLink
            {
                Id = Guid.NewGuid(),
                UserProfileId = userProfile.Id,
                PlatformName = GetPlatformName(command.Channel),
                ExternalPlatformId = command.PlatformUserId,
                WebhookConfirmationStatus = WebhookConfirmationStatus.Pending,
                LinkCreatedAt = DateTimeOffset.UtcNow,
                LastVerifiedAt = DateTimeOffset.UtcNow
            };

            await _identityLinkRepository.CreateAsync(identityLink, cancellationToken);

            _logger.LogInformation("Created new user profile {ProfileId} for {Platform} user {PlatformUserId}",
                userProfile.Id, identityLink.PlatformName, command.PlatformUserId);
        }
        else
        {
            userProfile = await _userProfileRepository.GetByIdAsync(identityLink.UserProfileId, cancellationToken)
                ?? throw new InvalidOperationException($"UserProfile {identityLink.UserProfileId} not found");

            // Update last active
            userProfile.LastActiveAt = DateTimeOffset.UtcNow;
            await _userProfileRepository.UpdateAsync(userProfile, cancellationToken);
        }

        // Get or create active session
        var activeSessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(userProfile.Id, cancellationToken);
        var activeSession = activeSessions.FirstOrDefault(s => s.ExpiresAt >= DateTimeOffset.UtcNow);

        if (activeSession == null)
        {
            // Create new session
            activeSession = new ConversationSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = userProfile.Id,
                Channel = command.Channel,
                Language = Language.English, // Will be detected from first message
                Status = SessionStatus.Active,
                StartTime = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
            };

            await _sessionRepository.CreateAsync(activeSession, cancellationToken);

            _logger.LogInformation("Created new session {SessionId} for user {ProfileId}",
                activeSession.Id, userProfile.Id);
        }

        // Buffer message for debouncing
        await BufferMessageAndProcessAsync(activeSession.Id, command, cancellationToken);
    }

    private async Task BufferMessageAndProcessAsync(Guid sessionId, ProcessWebhookCommand command, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var bufferKey = $"{BufferKeyPrefix}{sessionId}";
        var timerKey = $"{TimerKeyPrefix}{sessionId}";

        // Push current message to buffer
        await db.ListRightPushAsync(bufferKey, command.MessageText);

        // Try to set a timer key with NX (only if not exists)
        // If it succeeds, this task becomes the 'worker' that will process the buffer after delay
        var isTimerStarter = await db.StringSetAsync(timerKey, "active", DebounceWindow, When.NotExists);

        if (isTimerStarter)
        {
            // Background task to wait and then process
            // Note: In production, consider using a dedicated scheduler or queue with delayed visibility
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for the debounce window to pass
                    // We check KeyExists repeatedly to allow other requests to 'refresh' the timer
                    while (true)
                    {
                        await Task.Delay(500);
                        if (!await db.KeyExistsAsync(timerKey))
                        {
                            break;
                        }
                    }

                    // Use Lua script to fetch and clear atomically
                    // This ensures only one 'worker' can ever process a specific set of messages
                    var result = await db.ScriptEvaluateAsync(@"
                        local msgs = redis.call('LRANGE', KEYS[1], 0, -1)
                        if #msgs > 0 then
                            redis.call('DEL', KEYS[1])
                            return msgs
                        end
                        return nil",
                        [bufferKey]);

                    if (result.IsNull) return;

                    var messages = (RedisValue[])result!;
                    var combinedContent = string.Join("\n", messages.Select(m => m.ToString()));
                    _logger.LogInformation("Processing {Count} buffered messages for session {SessionId}", messages.Length, sessionId);

                    // Process combined message
                    var sendMessageCommand = new SendMessageCommand
                    {
                        SessionId = sessionId,
                        Content = combinedContent
                    };

                    await _sendMessageHandler.HandleAsync(sendMessageCommand, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing buffered messages for session {SessionId}", sessionId);
                }
            }, CancellationToken.None);
        }
        else
        {
            // refresh timer to extend the window if messages keep coming
            await db.KeyExpireAsync(timerKey, DebounceWindow);
            _logger.LogDebug("Extended debounce window for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Sends the response back to the messaging platform.
    /// </summary>
    /// <param name="command">The webhook command.</param>
    /// <param name="result">The message result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SendResponseToPlatformAsync(
        ProcessWebhookCommand command,
        SendMessageResult result,
        CancellationToken cancellationToken)
    {
        switch (command.Channel)
        {
            case Channel.Line:
                if (string.IsNullOrEmpty(command.ReplyToken))
                {
                    _logger.LogWarning("LINE reply token is missing");
                    return;
                }

                // For LINE, send as text message (Flex Message could be implemented later)
                await _lineClient.SendTextMessageAsync(command.ReplyToken, result.Content, cancellationToken);
                break;

            case Channel.Facebook:
            case Channel.Instagram:
            case Channel.WhatsApp:
                if (string.IsNullOrEmpty(command.RecipientId))
                {
                    _logger.LogWarning("Meta recipient ID is missing");
                    return;
                }

                var platform = command.Channel.ToString();
                await _metaClient.SendTextMessageAsync(command.RecipientId, result.Content, platform, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unsupported channel for webhook response: {Channel}", command.Channel);
                break;
        }
    }

    /// <summary>
    /// Maps channel to platform name.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The platform name.</returns>
    private static PlatformName GetPlatformName(Channel channel)
    {
        return channel switch
        {
            Channel.Line => PlatformName.Line,
            Channel.Facebook => PlatformName.Facebook,
            Channel.Instagram => PlatformName.Instagram,
            Channel.WhatsApp => PlatformName.WhatsApp,
            _ => throw new ArgumentException($"Unsupported channel: {channel}", nameof(channel))
        };
    }
}
