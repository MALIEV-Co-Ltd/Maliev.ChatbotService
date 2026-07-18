using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for linking an external platform identity to a user profile.
/// </summary>
public class LinkIdentityCommandHandler
{
    private readonly IIdentityLinkRepository _identityLinkRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<LinkIdentityCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkIdentityCommandHandler"/> class.
    /// </summary>
    /// <param name="identityLinkRepository">The identity link repository.</param>
    /// <param name="userProfileRepository">The user profile repository.</param>
    /// <param name="logger">The logger.</param>
    public LinkIdentityCommandHandler(
        IIdentityLinkRepository identityLinkRepository,
        IUserProfileRepository userProfileRepository,
        ILogger<LinkIdentityCommandHandler> logger)
    {
        _identityLinkRepository = identityLinkRepository;
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the LinkIdentityCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created identity link.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user profile is not found or identity link already exists.</exception>
    public async Task<LinkIdentityResult> HandleAsync(LinkIdentityCommand command, CancellationToken cancellationToken = default)
    {
        // Validate user profile exists
        var userProfile = await _userProfileRepository.GetByIdAsync(command.UserProfileId, cancellationToken);
        if (userProfile == null)
        {
            throw new InvalidOperationException($"User profile {command.UserProfileId} not found");
        }

        // Parse platform name
        var platformName = ParsePlatformName(command.PlatformName);

        // Check if identity link already exists
        var existingLinks = await _identityLinkRepository.GetByUserIdAsync(command.UserProfileId, cancellationToken);
        var existingLink = existingLinks.FirstOrDefault(l =>
            l.PlatformName == platformName &&
            l.ExternalPlatformId == command.ExternalUserId);

        if (existingLink != null)
        {
            _logger.LogWarning("Identity link already exists for user {UserProfileId} on platform {PlatformName}",
                command.UserProfileId, platformName);

            return new LinkIdentityResult
            {
                IdentityLinkId = existingLink.Id,
                PlatformName = platformName,
                ExternalUserId = command.ExternalUserId,
                WebhookConfirmationStatus = existingLink.WebhookConfirmationStatus,
                LinkCreatedAt = existingLink.LinkCreatedAt
            };
        }

        // Create new identity link
        var identityLink = new IdentityLink
        {
            Id = Guid.NewGuid(),
            UserProfileId = command.UserProfileId,
            PlatformName = platformName,
            ExternalPlatformId = command.ExternalUserId,
            WebhookConfirmationStatus = WebhookConfirmationStatus.Pending,
            LinkCreatedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow
        };

        var createdLink = await _identityLinkRepository.CreateAsync(identityLink, cancellationToken);

        _logger.LogInformation("Created identity link {IdentityLinkId} for user {UserProfileId} on platform {PlatformName}",
            createdLink.Id, command.UserProfileId, platformName);

        // Update user profile with the new external ID
        await UpdateUserProfileExternalIdAsync(userProfile, platformName, command.ExternalUserId, cancellationToken);

        return new LinkIdentityResult
        {
            IdentityLinkId = createdLink.Id,
            PlatformName = platformName,
            ExternalUserId = command.ExternalUserId,
            WebhookConfirmationStatus = createdLink.WebhookConfirmationStatus,
            LinkCreatedAt = createdLink.LinkCreatedAt
        };
    }

    private async Task UpdateUserProfileExternalIdAsync(UserProfile userProfile, PlatformName platformName, string externalUserId, CancellationToken cancellationToken)
    {
        switch (platformName)
        {
            case PlatformName.Line:
                if (string.IsNullOrEmpty(userProfile.LineUserId))
                {
                    userProfile.LineUserId = externalUserId;
                    await _userProfileRepository.UpdateAsync(userProfile, cancellationToken);
                }
                break;
            case PlatformName.Facebook:
                if (string.IsNullOrEmpty(userProfile.FacebookId))
                {
                    userProfile.FacebookId = externalUserId;
                    await _userProfileRepository.UpdateAsync(userProfile, cancellationToken);
                }
                break;
            case PlatformName.Instagram:
                if (string.IsNullOrEmpty(userProfile.InstagramId))
                {
                    userProfile.InstagramId = externalUserId;
                    await _userProfileRepository.UpdateAsync(userProfile, cancellationToken);
                }
                break;
            case PlatformName.WhatsApp:
                if (string.IsNullOrEmpty(userProfile.WhatsAppId))
                {
                    userProfile.WhatsAppId = externalUserId;
                    await _userProfileRepository.UpdateAsync(userProfile, cancellationToken);
                }
                break;
        }
    }

    private static PlatformName ParsePlatformName(string platformName)
    {
        return platformName.ToLowerInvariant() switch
        {
            "line" => PlatformName.Line,
            "facebook" => PlatformName.Facebook,
            "instagram" => PlatformName.Instagram,
            "whatsapp" => PlatformName.WhatsApp,
            _ => throw new ArgumentException($"Invalid platform name: {platformName}", nameof(platformName))
        };
    }
}

/// <summary>
/// Result of linking an identity.
/// </summary>
public class LinkIdentityResult
{
    /// <summary>
    /// Gets or sets the identity link ID.
    /// </summary>
    public Guid IdentityLinkId { get; set; }

    /// <summary>
    /// Gets or sets the platform name.
    /// </summary>
    public PlatformName PlatformName { get; set; }

    /// <summary>
    /// Gets or sets the external user ID.
    /// </summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the webhook confirmation status.
    /// </summary>
    public WebhookConfirmationStatus WebhookConfirmationStatus { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the link was created.
    /// </summary>
    public DateTimeOffset LinkCreatedAt { get; set; }
}
