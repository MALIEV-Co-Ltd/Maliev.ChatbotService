using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<ProcessWebhookCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessWebhookCommandHandler"/> class.
    /// </summary>
    /// <param name="userProfileRepository">The user profile repository.</param>
    /// <param name="identityLinkRepository">The identity link repository.</param>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="sendMessageHandler">The send message handler.</param>
    /// <param name="lineClient">The LINE client.</param>
    /// <param name="metaClient">The Meta client.</param>
    /// <param name="logger">The logger.</param>
    public ProcessWebhookCommandHandler(
        IUserProfileRepository userProfileRepository,
        IIdentityLinkRepository identityLinkRepository,
        IConversationSessionRepository sessionRepository,
        SendMessageCommandHandler sendMessageHandler,
        ILineClient lineClient,
        IMetaClient metaClient,
        ILogger<ProcessWebhookCommandHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _identityLinkRepository = identityLinkRepository;
        _sessionRepository = sessionRepository;
        _sendMessageHandler = sendMessageHandler;
        _lineClient = lineClient;
        _metaClient = metaClient;
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

        // Process the message through SendMessageHandler
        var sendMessageCommand = new SendMessageCommand
        {
            SessionId = activeSession.Id,
            Content = command.MessageText
        };

        var result = await _sendMessageHandler.HandleAsync(sendMessageCommand, cancellationToken);

        // Send response back to platform
        await SendResponseToPlatformAsync(command, result, cancellationToken);

        _logger.LogInformation("Webhook processed successfully for {Channel} user {UserId}",
            command.Channel, command.PlatformUserId);
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
