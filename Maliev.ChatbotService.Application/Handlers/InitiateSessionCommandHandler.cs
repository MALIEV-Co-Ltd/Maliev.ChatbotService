using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Messaging;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for initiating a new chatbot session.
/// </summary>
public class InitiateSessionCommandHandler
{
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IConversationSummaryService _summaryService;
    private readonly ILanguageDetectionService _languageDetectionService;
    private readonly IResponseFormatterService _responseFormatterService;
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<InitiateSessionCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitiateSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="userProfileRepository">The user profile repository.</param>
    /// <param name="summaryService">The conversation summary service.</param>
    /// <param name="languageDetectionService">The language detection service.</param>
    /// <param name="responseFormatterService">The response formatter service.</param>
    /// <param name="userMemoryRepository">The user memory repository.</param>
    /// <param name="eventPublisher">The event publisher.</param>
    /// <param name="logger">The logger.</param>
    public InitiateSessionCommandHandler(
        IConversationSessionRepository sessionRepository,
        IUserProfileRepository userProfileRepository,
        IConversationSummaryService summaryService,
        ILanguageDetectionService languageDetectionService,
        IResponseFormatterService responseFormatterService,
        IUserMemoryRepository userMemoryRepository,
        IEventPublisher eventPublisher,
        ILogger<InitiateSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _userProfileRepository = userProfileRepository;
        _summaryService = summaryService;
        _languageDetectionService = languageDetectionService;
        _responseFormatterService = responseFormatterService;
        _userMemoryRepository = userMemoryRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Handles the InitiateSessionCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created session response.</returns>
    public async Task<InitiateSessionResult> HandleAsync(InitiateSessionCommand command, CancellationToken cancellationToken = default)
    {
        // Parse channel
        var channel = ParseChannel(command.Channel);

        // Determine language
        var language = string.IsNullOrEmpty(command.Language)
            ? Language.English
            : ParseLanguage(command.Language);

        // Get or create user profile
        var userProfile = await GetOrCreateUserProfileAsync(channel, command.ExternalUserId, command.AuthenticatedUserProfileId, cancellationToken);

        // Retrieve previous session summaries for context continuity
        var previousSummaries = await _summaryService.GetRecentSummariesAsync(userProfile.Id, 3, cancellationToken);
        var summariesList = previousSummaries.ToList();

        // If user has previous sessions, detect language from most recent session
        if (string.IsNullOrEmpty(command.Language))
        {
            // First try to get language from most recent session with summary
            if (summariesList.Count > 0)
            {
                var mostRecentSessionWithSummary = await _sessionRepository.GetByIdAsync(summariesList[0].SessionId, cancellationToken);
                if (mostRecentSessionWithSummary != null)
                {
                    language = mostRecentSessionWithSummary.Language;
                    _logger.LogInformation("Detected language {Language} from previous session with summary for user {UserProfileId}", language, userProfile.Id);
                }
            }
            // If no summaries exist, check all previous sessions
            else
            {
                var previousSessions = await _sessionRepository.GetSessionsByUserIdAsync(userProfile.Id, cancellationToken);
                var mostRecentSession = previousSessions.FirstOrDefault();
                if (mostRecentSession != null)
                {
                    language = mostRecentSession.Language;
                    _logger.LogInformation("Detected language {Language} from previous session for user {UserProfileId}", language, userProfile.Id);
                }
            }
        }

        // Create new session
        var now = DateTimeOffset.UtcNow;
        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = channel,
            StartTime = now,
            LastActivityAt = now,
            ExpiresAt = now.AddHours(24),
            Language = language,
            Status = SessionStatus.Active
        };

        var createdSession = await _sessionRepository.CreateAsync(session, cancellationToken);

        _logger.LogInformation("Created new session {SessionId} for user {UserProfileId} on channel {Channel} with role {Role}",
            createdSession.Id, userProfile.Id, channel, userProfile.Role);

        // Publish ChatbotSessionCreatedEvent
        await _eventPublisher.PublishAsync(ChatbotEventFactory.SessionCreated(
            createdSession.Id,
            userProfile.Id,
            channel.ToString(),
            language.ToString(),
            createdSession.StartTime,
            createdSession.ExpiresAt), cancellationToken);

        // Get welcome message with context from previous summaries, user memories, and role
        var welcomeMessage = await BuildWelcomeMessageAsync(language, summariesList, userProfile.Id, userProfile.Role, cancellationToken);

        return new InitiateSessionResult
        {
            SessionId = createdSession.Id,
            WelcomeMessage = welcomeMessage,
            Language = language,
            ExpiresAt = createdSession.ExpiresAt
        };
    }

    private static Channel ParseChannel(string channel)
    {
        return channel.ToLowerInvariant() switch
        {
            "website" => Channel.Website,
            "line" => Channel.Line,
            "facebook" => Channel.Facebook,
            "instagram" => Channel.Instagram,
            "whatsapp" => Channel.WhatsApp,
            "intranet" => Channel.Intranet,
            "quote-engine" => Channel.QuoteEngine,
            _ => throw new ArgumentException($"Invalid channel: {channel}", nameof(channel))
        };
    }

    private static Language ParseLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "en" => Language.English,
            "th" => Language.Thai,
            _ => Language.English
        };
    }

    private async Task<UserProfile> GetOrCreateUserProfileAsync(Channel channel, string? externalUserId, Guid? authenticatedUserProfileId, CancellationToken cancellationToken)
    {
        UserProfile? userProfile = null;

        // First, check if user is authenticated and has a user profile ID
        if (authenticatedUserProfileId.HasValue)
        {
            userProfile = await _userProfileRepository.GetByIdAsync(authenticatedUserProfileId.Value, cancellationToken);
            if (userProfile == null)
            {
                userProfile = await _userProfileRepository.GetByInternalUserIdAsync(authenticatedUserProfileId.Value.ToString(), cancellationToken);
            }

            if (userProfile != null)
            {
                _logger.LogInformation("Using authenticated user profile {UserProfileId}", authenticatedUserProfileId.Value);
            }
        }

        // If not authenticated, try to find existing user profile by external user ID
        if (userProfile == null && !string.IsNullOrEmpty(externalUserId))
        {
            userProfile = channel switch
            {
                Channel.Line => await _userProfileRepository.GetByLineUserIdAsync(externalUserId, cancellationToken),
                Channel.Facebook => await _userProfileRepository.GetByFacebookIdAsync(externalUserId, cancellationToken),
                Channel.Instagram => await _userProfileRepository.GetByInstagramIdAsync(externalUserId, cancellationToken),
                Channel.WhatsApp => await _userProfileRepository.GetByWhatsAppIdAsync(externalUserId, cancellationToken),
                _ => null
            };
        }

        // Create new user profile if not found
        if (userProfile == null)
        {
            // Detect if user is internal agent (intranet channel = internal agent)
            var role = channel == Channel.Intranet ? UserRole.InternalAgent : UserRole.Customer;

            userProfile = new UserProfile
            {
                Id = authenticatedUserProfileId ?? Guid.NewGuid(),
                InternalUserId = authenticatedUserProfileId?.ToString(),
                LineUserId = channel == Channel.Line ? externalUserId : null,
                FacebookId = channel == Channel.Facebook ? externalUserId : null,
                InstagramId = channel == Channel.Instagram ? externalUserId : null,
                WhatsAppId = channel == Channel.WhatsApp ? externalUserId : null,
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow,
                LastActiveAt = DateTimeOffset.UtcNow
            };

            userProfile = await _userProfileRepository.CreateAsync(userProfile, cancellationToken);
            _logger.LogInformation("Created new user profile {UserProfileId} for channel {Channel} with role {Role}", userProfile.Id, channel, role);
        }
        else
        {
            if (authenticatedUserProfileId.HasValue && string.IsNullOrWhiteSpace(userProfile.InternalUserId))
            {
                userProfile.InternalUserId = authenticatedUserProfileId.Value.ToString();
            }

            // Update last active time
            userProfile.LastActiveAt = DateTimeOffset.UtcNow;
            await _userProfileRepository.UpdateAsync(userProfile, cancellationToken);
        }

        return userProfile;
    }

    private async Task<string> BuildWelcomeMessageAsync(Language language, List<ConversationSummary> previousSummaries, Guid userProfileId, UserRole role, CancellationToken cancellationToken)
    {
        // For internal agents, provide CRM-focused welcome message
        if (role == UserRole.InternalAgent)
        {
            return language == Language.Thai
                ? "สวัสดีครับ ผมพร้อมช่วยคุณในการค้นหาข้อมูล CRM, สถานะใบเสนอราคา, คำสั่งซื้อ และอัพเดทข้อมูลลูกค้า มีอะไรให้ผมช่วยไหมครับ?"
                : "Hello! I'm ready to assist you with CRM queries, quotation status, order information, and customer updates. How can I help you today?";
        }

        var baseWelcomeMessage = _responseFormatterService.GetWelcomeMessage(language);

        // Retrieve high-confidence user memories
        var userMemories = await _userMemoryRepository.GetHighConfidenceMemoriesAsync(userProfileId, 0.8, cancellationToken);
        var contextParts = new List<string>();

        // Extract context from user memories first (most relevant)
        foreach (var memory in userMemories.Take(3))
        {
            try
            {
                var memoryValue = JsonDocument.Parse(memory.Value);
                var root = memoryValue.RootElement;

                // Extract all string values from the memory JSON
                var values = new List<string>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = prop.Value.GetString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            values.Add(value);
                        }
                    }
                }

                if (values.Count > 0)
                {
                    contextParts.Add(string.Join(", ", values));
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse user memory {MemoryId}", memory.Id);
            }
        }

        // Extract context from previous summaries if no memories
        if (contextParts.Count == 0 && previousSummaries.Count > 0)
        {
            foreach (var summary in previousSummaries.Take(1)) // Use only the most recent summary
            {
                try
                {
                    var summaryDoc = JsonDocument.Parse(summary.StructuredSummary);
                    var root = summaryDoc.RootElement;

                    // Extract topics
                    if (root.TryGetProperty("topics", out var topics))
                    {
                        var topicsList = topics.EnumerateArray()
                            .Select(t => t.GetString())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();

                        if (topicsList.Count > 0)
                        {
                            contextParts.Add(string.Join(", ", topicsList));
                        }
                    }

                    // Extract preferences
                    if (root.TryGetProperty("preferences", out var preferences))
                    {
                        var prefsList = preferences.EnumerateArray()
                            .Select(p => p.GetString())
                            .Where(p => !string.IsNullOrEmpty(p))
                            .ToList();

                        if (prefsList.Count > 0)
                        {
                            contextParts.Add(string.Join(", ", prefsList));
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse summary {SummaryId}", summary.Id);
                }
            }
        }

        if (contextParts.Count > 0)
        {
            var contextSummary = string.Join(". ", contextParts);
            if (language == Language.Thai)
            {
                return $"{baseWelcomeMessage} ฉันจำได้ว่าคุณสนใจเรื่อง {contextSummary}";
            }
            else
            {
                return $"{baseWelcomeMessage} I remember you were interested in {contextSummary.ToLowerInvariant()}.";
            }
        }

        return baseWelcomeMessage;
    }
}

/// <summary>
/// Result of initiating a session.
/// </summary>
public class InitiateSessionResult
{
    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the welcome message.
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    public Language Language { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
