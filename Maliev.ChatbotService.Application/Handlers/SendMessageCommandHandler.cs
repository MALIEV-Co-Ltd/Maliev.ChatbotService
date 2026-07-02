using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Costing;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Validators;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for sending a message in a chatbot session.
/// </summary>
public class SendMessageCommandHandler
{
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IKnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IConversationSummaryService _summaryService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IUsageBudgetService _usageBudgetService;
    private readonly IGeminiClient _geminiClient;
    private readonly IModelContextCacheService _modelContextCacheService;
    private readonly IModelFileStagingService _modelFileStagingService;
    private readonly ISystemInstructionService _systemInstructionService;
    private readonly IIntentClassificationService _intentClassificationService;
    private readonly ILanguageDetectionService _languageDetectionService;
    private readonly IResponseFormatterService _responseFormatterService;
    private readonly IOperationExecutionService? _operationExecutionService;
    private readonly BusinessConstraintValidator _businessConstraintValidator;
    private readonly IOperationLogRepository _operationLogRepository;
    private readonly IConversationMetrics _metrics;
    private readonly IEventPublisher _eventPublisher;
    private readonly ISearchDomainLogRepository _searchDomainLogRepository;
    private readonly IWebSearchService _webSearchService;
    private readonly AgentChatHandler _agentChatHandler;
    private readonly IToolExecutorService _toolExecutor;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;
    private readonly ILogger<SendMessageCommandHandler> _logger;
    private readonly bool _webSearchGloballyEnabled;
    private readonly bool _urlContextEnabled;
    private readonly int _urlContextMaxUrls;
    private readonly long _fileApiInlineThresholdBytes;
    private readonly long _maxImageSizeBytes;
    private readonly long _maxPdfSizeBytes;
    private readonly long _maxVideoSizeBytes;
    private readonly long _maxAudioSizeBytes;
    private readonly string _defaultChatModelName;
    private readonly string _chatImageMediaResolution;
    private readonly string _chatPdfMediaResolution;
    private readonly string _chatVideoMediaResolution;
    private readonly bool _agentIncludeThoughts;
    private readonly int _agentThinkingBudgetTokens;

    // Must exceed the worst-case agent loop so the per-session lock cannot expire mid-turn and let a
    // concurrent message interleave (C2). AgentChatHandler runs up to MaxIterations (10) iterations,
    // each with a per-call timeout of 30s, so the worst case is ~300s; 330s adds a safety margin.
    private const int SessionLockSeconds = 330;
    private const int ChatMaxOutputTokens = 2048;
    private const int ChatMaxPromptTokens = 30000;
    private const int DefaultAgentThinkingBudgetTokens = 1024;
    private const int MaxAgentThinkingBudgetTokens = 4096;
    private const int MaxStructuredOutputSchemaJsonCharacters = 16_384;
    private const long DefaultFileApiInlineThresholdBytes = 5L * 1024 * 1024;
    private const int DefaultMaxImageSizeMb = 10;
    private const int DefaultMaxPdfSizeMb = 20;
    private const int DefaultMaxVideoSizeMb = 50;
    private const int DefaultMaxAudioSizeMb = 10;
    private const int DefaultUrlContextMaxUrls = 3;
    private const string DefaultChatImageMediaResolution = "MEDIA_RESOLUTION_MEDIUM";
    private const string DefaultChatPdfMediaResolution = "MEDIA_RESOLUTION_MEDIUM";
    private const string DefaultChatVideoMediaResolution = "MEDIA_RESOLUTION_LOW";
    private const string JsonResponseMimeType = "application/json";
    private static readonly string[] AgentToolKeywords =
    [
        "lookup", "look up", "find", "search", "show", "list", "get ",
        "check", "create", "update", "delete", "save", "send", "generate",
        "revise", "convert", "download", "upload", "attach", "open",
        "quote", "quotation", "rfq", "estimate", "pricing", "price this",
        "order", "invoice", "payment", "receipt", "customer", "supplier",
        "project", "status", "history", "audit", "activity", "reminder",
        "connector", "handoff", "google drive", "drive file", "authenticate",
        "cad", "3d", "preview", "drawing", "stl", "step", "dxf", "mesh",
        "inventory", "availability", "stock"
    ];

    private static readonly HashSet<string> AllowedChatModelOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="SendMessageCommandHandler"/> class.
    /// </summary>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="messageRepository">The message repository.</param>
    /// <param name="userProfileRepository">The user profile repository.</param>
    /// <param name="knowledgeBaseRepository">The knowledge base repository.</param>
    /// <param name="summaryService">The conversation summary service.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    /// <param name="usageBudgetService">The daily token budget service.</param>
    /// <param name="geminiClient">The Gemini API client.</param>
    /// <param name="modelContextCacheService">The model context cache service.</param>
    /// <param name="modelFileStagingService">The model file staging service.</param>
    /// <param name="systemInstructionService">The system instruction service.</param>
    /// <param name="intentClassificationService">The intent classification service.</param>
    /// <param name="languageDetectionService">The language detection service.</param>
    /// <param name="responseFormatterService">The response formatter service.</param>
    /// <param name="operationExecutionService">The operation execution service (optional for internal agents).</param>
    /// <param name="businessConstraintValidator">The business constraint validator.</param>
    /// <param name="operationLogRepository">The operation log repository.</param>
    /// <param name="metrics">The conversation metrics.</param>
    /// <param name="eventPublisher">The event publisher.</param>
    /// <param name="searchDomainLogRepository">The search domain log repository.</param>
    /// <param name="webSearchService">The web search service.</param>
    /// <param name="agentChatHandler">The agent chat handler for function calling.</param>
    /// <param name="toolExecutor">The tool executor service.</param>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">Application configuration.</param>
    public SendMessageCommandHandler(
        IConversationSessionRepository sessionRepository,
        IMessageRepository messageRepository,
        IUserProfileRepository userProfileRepository,
        IKnowledgeBaseRepository knowledgeBaseRepository,
        IConversationSummaryService summaryService,
        IRateLimitService rateLimitService,
        IUsageBudgetService usageBudgetService,
        IGeminiClient geminiClient,
        IModelContextCacheService modelContextCacheService,
        IModelFileStagingService modelFileStagingService,
        ISystemInstructionService systemInstructionService,
        IIntentClassificationService intentClassificationService,
        ILanguageDetectionService languageDetectionService,
        IResponseFormatterService responseFormatterService,
        IOperationExecutionService? operationExecutionService,
        BusinessConstraintValidator businessConstraintValidator,
        IOperationLogRepository operationLogRepository,
        IConversationMetrics metrics,
        IEventPublisher eventPublisher,
        ISearchDomainLogRepository searchDomainLogRepository,
        IWebSearchService webSearchService,
        AgentChatHandler agentChatHandler,
        IToolExecutorService toolExecutor,
        StackExchange.Redis.IConnectionMultiplexer redis,
        ILogger<SendMessageCommandHandler> logger,
        IConfiguration? configuration = null)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _userProfileRepository = userProfileRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _summaryService = summaryService;
        _rateLimitService = rateLimitService;
        _usageBudgetService = usageBudgetService;
        _geminiClient = geminiClient;
        _modelContextCacheService = modelContextCacheService;
        _modelFileStagingService = modelFileStagingService;
        _systemInstructionService = systemInstructionService;
        _intentClassificationService = intentClassificationService;
        _languageDetectionService = languageDetectionService;
        _responseFormatterService = responseFormatterService;
        _operationExecutionService = operationExecutionService;
        _businessConstraintValidator = businessConstraintValidator;
        _operationLogRepository = operationLogRepository;
        _metrics = metrics;
        _eventPublisher = eventPublisher;
        _searchDomainLogRepository = searchDomainLogRepository;
        _webSearchService = webSearchService;
        _agentChatHandler = agentChatHandler;
        _toolExecutor = toolExecutor;
        _redis = redis;
        _logger = logger;
        _webSearchGloballyEnabled = configuration?.GetValue<bool>("Features:WebSearchEnabled") ?? false;
        _urlContextEnabled = configuration?.GetValue<bool>("Gemini:UrlContext:Enabled") ?? false;
        _urlContextMaxUrls = Math.Clamp(
            configuration?.GetValue<int?>("Gemini:UrlContext:MaxUrlsPerRequest") ?? DefaultUrlContextMaxUrls,
            1,
            20);
        _fileApiInlineThresholdBytes = Math.Max(
            0,
            configuration?.GetValue<long?>("Gemini:FileApiInlineThresholdBytes") ??
                DefaultFileApiInlineThresholdBytes);
        _maxImageSizeBytes = ResolveMaxFileSizeBytes(configuration, "FileUploadLimits:MaxImageSizeMB", DefaultMaxImageSizeMb);
        _maxPdfSizeBytes = ResolveMaxFileSizeBytes(configuration, "FileUploadLimits:MaxPdfSizeMB", DefaultMaxPdfSizeMb);
        _maxVideoSizeBytes = ResolveMaxFileSizeBytes(configuration, "FileUploadLimits:MaxVideoSizeMB", DefaultMaxVideoSizeMb);
        _maxAudioSizeBytes = ResolveMaxFileSizeBytes(configuration, "FileUploadLimits:MaxAudioSizeMB", DefaultMaxAudioSizeMb);
        _defaultChatModelName = ResolveDefaultChatModelName(configuration);
        _chatImageMediaResolution = ResolveConfiguredMediaResolution(
            configuration?["Gemini:Chat:ImageMediaResolution"],
            "Gemini:Chat:ImageMediaResolution",
            DefaultChatImageMediaResolution);
        _chatPdfMediaResolution = ResolveConfiguredMediaResolution(
            configuration?["Gemini:Chat:PdfMediaResolution"],
            "Gemini:Chat:PdfMediaResolution",
            DefaultChatPdfMediaResolution);
        _chatVideoMediaResolution = ResolveConfiguredMediaResolution(
            configuration?["Gemini:Chat:VideoMediaResolution"],
            "Gemini:Chat:VideoMediaResolution",
            DefaultChatVideoMediaResolution);
        _agentIncludeThoughts = configuration?.GetValue<bool?>("Gemini:Agent:IncludeThoughts") ?? false;
        _agentThinkingBudgetTokens = Math.Clamp(
            configuration?.GetValue<int?>("Gemini:Agent:ThinkingBudgetTokens") ??
                DefaultAgentThinkingBudgetTokens,
            0,
            MaxAgentThinkingBudgetTokens);
    }

    /// <summary>
    /// Handles the SendMessageCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message response.</returns>
    /// <exception cref="InvalidOperationException">Thrown when session is not found or rate limit is exceeded.</exception>
    public async Task<SendMessageResult> HandleAsync(SendMessageCommand command, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Ensure sequential processing per session using Redis distributed lock
        var db = _redis.GetDatabase();
        var lockKey = $"chatbot:lock:{command.SessionId}";
        var lockValue = Guid.NewGuid().ToString();

        // Hold the lock long enough to cover the worst-case agent loop (see SessionLockSeconds).
        if (!await db.LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(SessionLockSeconds)))
        {
            _logger.LogWarning("Could not acquire lock for session {SessionId}. System busy.", command.SessionId);
            throw new InvalidOperationException("System is busy processing your previous message. Please wait a moment.");
        }

        var stagedFileNames = new List<string>();

        try
        {
            // Get session
            var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {command.SessionId} not found");
            }

            // Check if session is expired
            if (session.ExpiresAt < DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException($"Session {command.SessionId} has expired");
            }

            // 2. Atomic rate limit increment and check (100 msg/hr)
            var currentCount = await _rateLimitService.IncrementMessageCountAsync(session.UserProfileId, cancellationToken);
            if (currentCount > 100)
            {
                // Publish ChatbotRateLimitExceededEvent
                await _eventPublisher.PublishAsync(new ChatbotRateLimitExceededEvent
                {
                    MessageId = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    Source = "ChatbotService",
                    CorrelationId = session.Id,
                    UserProfileId = session.UserProfileId,
                    SessionId = session.Id,
                    Channel = session.Channel.ToString(),
                    CurrentMessageCount = currentCount,
                    RateLimitThreshold = 100,
                    ResetAt = DateTimeOffset.UtcNow.AddHours(1)
                }, cancellationToken);

                _logger.LogWarning("Rate limit exceeded for user {UserProfileId} in session {SessionId}", session.UserProfileId, session.Id);
                throw new InvalidOperationException("Rate limit exceeded. Please try again later.");
            }

            // Validate and normalize customer input (S3): length guard + null-byte strip. Runs after
            // the rate-limit increment so abuse attempts still count against the budget.
            if (!MessagePipelinePolicy.TryNormalizeContent(command.Content, out var normalizedContent, out var contentError))
            {
                _logger.LogWarning("Rejected oversized message content for session {SessionId}", session.Id);
                throw new InvalidOperationException(contentError);
            }
            command.Content = normalizedContent;

            // Validate attachment sizes
            if (command.Attachments != null && command.Attachments.Count > 0)
            {
                foreach (var attachment in command.Attachments)
                {
                    if (!MessagePipelinePolicy.TryValidateGeminiAttachmentReference(
                            attachment.Data,
                            attachment.MimeType,
                            attachment.SizeBytes,
                            out var referenceError))
                    {
                        throw new InvalidOperationException(referenceError);
                    }

                    var maxSize = attachment.ContentType switch
                    {
                        ContentType.Image => _maxImageSizeBytes,
                        ContentType.PDF => _maxPdfSizeBytes,
                        ContentType.Video => _maxVideoSizeBytes,
                        ContentType.Audio => _maxAudioSizeBytes,
                        _ => _maxImageSizeBytes
                    };

                    if (attachment.SizeBytes > maxSize)
                    {
                        var maxSizeMB = maxSize / (1024 * 1024);
                        throw new InvalidOperationException($"Attachment size exceeds the maximum allowed size of {maxSizeMB}MB for {attachment.ContentType} files.");
                    }
                }

                // Cap attachment count and combined size to bound per-message cost (S2).
                if (!MessagePipelinePolicy.TryValidateAttachmentBudget(
                        command.Attachments.Count,
                        command.Attachments.Sum(a => a.SizeBytes),
                        out var attachmentBudgetError))
                {
                    _logger.LogWarning("Rejected message with excessive attachments for session {SessionId}", session.Id);
                    throw new InvalidOperationException(attachmentBudgetError);
                }
            }

            // Prefer an explicit caller-selected response language; fall back to message detection for
            // legacy clients that do not send one.
            var detectedLanguage = ResolveMessageLanguage(command.Language, command.Content);

            // Update session language if different
            if (session.Language != detectedLanguage)
            {
                session.Language = detectedLanguage;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                _logger.LogInformation("Updated session {SessionId} language to {Language}", session.Id, detectedLanguage);
            }

            // Daily token/cost budget (S2): a soft, per-user rolling-24h ceiling on model tokens that
            // bounds cost where the hourly message count can't (one turn can fan out to many model
            // calls with large payloads). Checked after the hourly increment — so refused attempts
            // still consume that quota — but before any model call, so an over-budget user costs
            // nothing further. Refused gracefully in-band (no model call, no message persisted).
            var usageSnapshot = await _usageBudgetService.GetDailyTokenUsageSnapshotAsync(session.UserProfileId, cancellationToken);
            if (usageSnapshot.IsExceeded)
            {
                _logger.LogWarning("Daily token budget exceeded for user {UserProfileId} in session {SessionId}",
                    session.UserProfileId, session.Id);

                return new SendMessageResult
                {
                    MessageId = Guid.NewGuid(),
                    Content = MessagePipelinePolicy.BuildDailyBudgetExceededMessage(detectedLanguage),
                    Role = MessageRole.Assistant,
                    Language = detectedLanguage,
                    CreatedAt = DateTimeOffset.UtcNow,
                    SessionId = session.Id,
                    UsageSnapshot = usageSnapshot
                };
            }

            // Save user message, persisting URL/GCS attachment references so later turns keep context (C3).
            var userMessage = new Message
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = command.Content,
                ContentType = ContentType.Text,
                CreatedAt = DateTimeOffset.UtcNow,
                MetadataJson = MessagePipelinePolicy.BuildAttachmentMetadataJson(
                    command.Attachments?.Select(a => (a.MimeType, a.Data)).ToList())
            };

            await _messageRepository.CreateAsync(userMessage, cancellationToken);



            // Check if user is internal agent and message contains operation intent


            var userProfile = await _userProfileRepository.GetByIdAsync(session.UserProfileId, cancellationToken);
            if (userProfile?.Role == UserRole.InternalAgent && _operationExecutionService != null)
            {
                var operationIntent = DetectOperationIntent(command.Content);
                if (operationIntent != null)
                {
                    _logger.LogInformation("Detected operation intent {OperationType} for internal agent in session {SessionId}",
                        operationIntent.OperationType, session.Id);

                    var operationResult = await _operationExecutionService.ExecuteOperationAsync(
                        operationIntent.OperationType,
                        operationIntent.Parameters,
                        cancellationToken);

                    if (operationResult.Success)
                    {
                        // Save operation result as assistant message
                        var operationMessage = new Message
                        {
                            Id = Guid.NewGuid(),
                            SessionId = session.Id,
                            Role = MessageRole.Assistant,
                            Content = operationResult.FormattedMessage ?? "Operation completed successfully",
                            ContentType = ContentType.Text,
                            CreatedAt = DateTimeOffset.UtcNow
                        };

                        await _messageRepository.CreateAsync(operationMessage, cancellationToken);

                        // Update session
                        session.LastActivityAt = DateTimeOffset.UtcNow;
                        session.ExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
                        await _sessionRepository.UpdateAsync(session, cancellationToken);

                        stopwatch.Stop();
                        _metrics.RecordConversation();
                        _metrics.RecordResponseLatency(stopwatch.Elapsed.TotalMilliseconds);

                        // Publish ChatbotMessageReceivedEvent for operation execution
                        await _eventPublisher.PublishAsync(new ChatbotMessageReceivedEvent
                        {
                            MessageId = Guid.NewGuid(),
                            Timestamp = DateTimeOffset.UtcNow,
                            Source = "ChatbotService",
                            CorrelationId = session.Id,
                            SessionId = session.Id,
                            UserProfileId = session.UserProfileId,
                            Channel = session.Channel.ToString(),
                            Language = detectedLanguage.ToString(),
                            UserMessageContent = command.Content,
                            AssistantResponseContent = operationMessage.Content,
                            ResponseLatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                            ReceivedAt = userMessage.CreatedAt
                        }, cancellationToken);

                        // Convert OperationActions to SuggestedActionDto
                        var suggestedActionsDto = operationResult.SuggestedActions.Select(a => new SuggestedActionDto
                        {
                            Text = a.Label,
                            Type = a.ActionType,
                            Data = a.Parameters != null ? JsonSerializer.Serialize(a.Parameters) : null
                        }).ToList();

                        return new SendMessageResult
                        {
                            MessageId = operationMessage.Id,
                            Content = operationMessage.Content,
                            Role = MessageRole.Assistant,
                            Language = detectedLanguage,
                            SuggestedActions = suggestedActionsDto,
                            CreatedAt = operationMessage.CreatedAt
                        };
                    }
                }
            }

            // Get conversation history
            var conversationHistory = await _messageRepository.GetRecentBySessionIdAsync(session.Id, 10, cancellationToken);

            // Get previous session summaries for context
            var previousSummaries = await _summaryService.GetRecentSummariesAsync(session.UserProfileId, 2, cancellationToken);
            var summariesContext = BuildSummariesContext(previousSummaries);

            // 1. Classify intent only when the result can affect the prompt. Customer-facing channels
            // intentionally discard intranet topic keys, so calling Gemini there burns tokens for no
            // prompt benefit.
            var classification = MessagePipelinePolicy.AllowsDomainTopicInjection(session.Channel)
                ? await _intentClassificationService.ClassifyIntentAsync(command.Content, cancellationToken)
                : new IntentClassificationResult { Intent = "General", Confidence = 0.0 };
            _metrics.RecordIntentClassification(classification.Intent, classification.Confidence);

            // Channel-scoped topic injection (P1): customer-facing channels (Website/QuoteEngine/social)
            // must never receive intranet domain topic prompts or knowledge-base facts. The intent
            // classifier is intranet-oriented, so its topics are only injected for the intranet channel.
            var topicKeys = MessagePipelinePolicy.BuildInjectableTopicKeys(session.Channel, classification);
            foreach (var topic in topicKeys)
            {
                _metrics.RecordContextInjection("Topic", topic);
            }

            // 2. Get merged instructions using the channel-specific core prompt profile.
            var coreInstructionTopicKey = GetCoreInstructionTopicKey(session.Channel);
            var systemInstructionText = await _systemInstructionService.GetMergedInstructionsAsync(
                topicKeys,
                coreInstructionTopicKey,
                cancellationToken);
            var dynamicContextSections = new List<string>();

            // 3. Fetch Knowledge Base Facts
            var injectedKnowledgeIds = new List<Guid>();
            if (topicKeys.Any())
            {
                var facts = new List<KnowledgeBase>();
                foreach (var topic in topicKeys)
                {
                    var topicFacts = await _knowledgeBaseRepository.GetByTopicAsync(topic, cancellationToken);
                    foreach (var fact in topicFacts)
                    {
                        facts.Add(fact);
                        _metrics.RecordContextInjection("KnowledgeBase", fact.TopicKey);
                    }
                }

                if (facts.Any())
                {
                    var factLines = new List<string> { "## RELEVANT FACTS AND CONTEXT" };
                    foreach (var fact in facts)
                    {
                        factLines.Add($"- {fact.Content}");
                        injectedKnowledgeIds.Add(fact.Id);
                    }

                    dynamicContextSections.Add(string.Join("\n", factLines));
                }
            }

            // Keep volatile per-user context out of systemInstruction so Gemini can reuse the stable
            // channel prompt prefix for implicit caching.
            if (!string.IsNullOrEmpty(summariesContext))
            {
                dynamicContextSections.Add($"Previous conversation context:\n{summariesContext}");
            }

            // Get system instruction for business constraint validation (Core only for now)
            var coreInstruction = await _systemInstructionService.GetActiveInstructionAsync(coreInstructionTopicKey, cancellationToken);

            // Check if Gemini built-in search should be triggered
            bool enableGeminiSearch = false;
            if (coreInstruction != null &&
                _webSearchGloballyEnabled &&
                coreInstruction.EnableWebSearch &&
                ShouldTriggerWebSearch(command.Content))
            {
                var competitorKeywords = new[] { "competitor", "pricing", "cost comparison", "price comparison" };
                var isCompetitorQuery = competitorKeywords.Any(k => command.Content.ToLowerInvariant().Contains(k));

                if (!isCompetitorQuery)
                {
                    enableGeminiSearch = true;
                    _logger.LogInformation("Gemini built-in search enabled for query: {Query}", command.Content);
                }
            }

            // Build Gemini request, re-hydrating persisted attachment references for prior user turns (C3).
            var geminiMessages = conversationHistory
                .OrderBy(m => m.CreatedAt)
                .Select(m =>
                {
                    var persistedAttachments = m.Role == MessageRole.User
                        ? MessagePipelinePolicy.ParsePersistedAttachments(m.MetadataJson)
                        : [];
                    return new GeminiMessage
                    {
                        Role = m.Role == MessageRole.User ? "user" : "assistant",
                        Content = m.Content,
                        Attachments = persistedAttachments.Count > 0 ? persistedAttachments : null
                    };
                })
                .ToList();

            var dynamicContextMessage = BuildDynamicContextMessage(dynamicContextSections);
            if (dynamicContextMessage is not null)
            {
                InsertDynamicContextBeforeLatestUserTurn(geminiMessages, dynamicContextMessage);
            }

            // Attach files to the current user message. Persisted refs cover prior turns; the current
            // turn's full attachments (including inline data) are re-attached from the command.
            if (command.Attachments is { Count: > 0 } &&
                geminiMessages.Count > 0 &&
                geminiMessages[geminiMessages.Count - 1].Role == "user")
            {
                var currentTurnAttachments = new List<GeminiAttachment>();
                for (var i = 0; i < command.Attachments.Count; i++)
                {
                    currentTurnAttachments.Add(await BuildCurrentTurnAttachmentAsync(
                        command.Attachments[i],
                        i + 1,
                        stagedFileNames,
                        cancellationToken));
                }

                geminiMessages[geminiMessages.Count - 1].Attachments = currentTurnAttachments;
            }

            var structuredOutput = ResolveStructuredOutput(command.ResponseMimeType, command.ResponseSchema);
            var isAgentToolCandidate = ShouldAttachAgentTools(session.Channel, command.Content, command.Attachments) &&
                string.IsNullOrEmpty(structuredOutput.ResponseMimeType);
            List<GeminiToolDeclaration> tools = isAgentToolCandidate
                ? _toolExecutor.GetToolDeclarations(GetToolProfile(session.Channel))
                : [];
            var hasAgentTools = tools.Count > 0;
            var includeAgentThoughts = hasAgentTools && _agentIncludeThoughts;
            var enableGeminiUrlContext = ShouldEnableUrlContext(
                command.Content,
                _urlContextEnabled,
                _urlContextMaxUrls,
                hasAgentTools);
            if (enableGeminiUrlContext)
            {
                enableGeminiSearch = false;
            }

            var geminiRequest = new GeminiRequest
            {
                ModelName = ResolveChatModelName(command.ModelName),
                SystemInstruction = systemInstructionText,
                Messages = geminiMessages,
                TimeoutSeconds = enableGeminiSearch || enableGeminiUrlContext ? 30 : 10,
                ResponseMimeType = structuredOutput.ResponseMimeType,
                ResponseSchema = structuredOutput.ResponseSchema,
                MaxTokens = ChatMaxOutputTokens,
                MaxPromptTokens = ChatMaxPromptTokens,
                EnableWebSearch = enableGeminiSearch,
                EnableUrlContext = enableGeminiUrlContext,
                IncludeThoughts = includeAgentThoughts,
                ThinkingBudget = includeAgentThoughts ? _agentThinkingBudgetTokens : 0,
                MediaResolution = ResolveMediaResolution(geminiMessages),
                Store = false
            };

            var cacheReference = await _modelContextCacheService.GetOrCreateSystemInstructionCacheAsync(
                new ModelContextCacheRequest
                {
                    ModelName = geminiRequest.ModelName,
                    SystemInstruction = systemInstructionText
                },
                cancellationToken);
            if (cacheReference is not null)
            {
                geminiRequest.CachedContentName = cacheReference.CachedContentName;
                geminiRequest.SystemInstruction = string.Empty;
            }

            // Use the agent loop only for channels with scoped, allowlisted tool profiles.
            GeminiResponse geminiResponse;
            var thinkingSteps = new List<Models.ThinkingStep>();

            if (hasAgentTools)
            {
                geminiRequest.Tools = tools;
                geminiRequest.ToolConfig = new GeminiFunctionCallingConfig { Mode = "AUTO" };
                geminiRequest.TimeoutSeconds = 30;

                var agentResult = await _agentChatHandler.ExecuteAsync(
                    geminiRequest,
                    command.ThinkingStepCallback,
                    command.UserToken,
                    command.QuoteAgentContextToken,
                    command.TextDeltaCallback,
                    command.ThoughtDeltaCallback,
                    cancellationToken);

                thinkingSteps = agentResult.ThinkingSteps;
                geminiResponse = new GeminiResponse
                {
                    Success = agentResult.Success,
                    Content = agentResult.Content,
                    ErrorMessage = agentResult.ErrorMessage,
                    IsFallback = agentResult.IsFallback,
                    TokenUsage = agentResult.TokenUsage,
                    ServiceTier = agentResult.ServiceTier,
                    GroundingWebSearchQueries = agentResult.GroundingWebSearchQueries
                };
            }
            else
            {
                geminiResponse = await SendGeminiMaybeStreamingAsync(
                    geminiRequest,
                    command.TextDeltaCallback,
                    command.ThoughtDeltaCallback,
                    cancellationToken);
            }

            if (!geminiResponse.Success && !geminiResponse.IsFallback)
            {
                _logger.LogError("Gemini API call failed: {ErrorMessage}", geminiResponse.ErrorMessage);
                throw new InvalidOperationException("Failed to generate response from AI service");
            }



            // Format response with suggested actions

            var (formattedContent, suggestedActions) = _responseFormatterService.FormatResponse(geminiResponse.Content, detectedLanguage);



            Guid messageId = Guid.NewGuid();

            DateTimeOffset createdAt = DateTimeOffset.UtcNow;



            GeminiCostEstimate? costEstimate = null;

            if (geminiResponse.Success)

            {
                costEstimate = GeminiCostEstimator.Estimate(
                    geminiRequest.ModelName ?? _defaultChatModelName,
                    geminiResponse.ServiceTier ?? geminiRequest.ServiceTier,
                    geminiResponse.TokenUsage,
                    GetGoogleSearchGroundingPromptCount(geminiResponse));

                // Save assistant message ONLY if it was a successful AI response

                var assistantMessage = new Message

                {

                    Id = Guid.NewGuid(),

                    SessionId = session.Id,

                    Role = MessageRole.Assistant,

                    Content = formattedContent,

                    ContentType = ContentType.Text,

                    CreatedAt = DateTimeOffset.UtcNow,

                    MetadataJson = JsonSerializer.Serialize(new

                    {

                        intent = classification.Intent,

                        confidence = classification.Confidence,

                        injectedTopicKeys = topicKeys,

                        injectedKnowledgeIds = injectedKnowledgeIds,

                        tokenUsage = BuildTokenUsageMetadata(geminiResponse.TokenUsage),

                        serviceTier = geminiResponse.ServiceTier,

                        groundingMetadata = BuildGroundingMetadata(geminiResponse),

                        costEstimate = BuildCostEstimateMetadata(costEstimate)

                    })

                };



                await _messageRepository.CreateAsync(assistantMessage, cancellationToken);

                messageId = assistantMessage.Id;

                createdAt = assistantMessage.CreatedAt;

            }



            // Record token consumption against the user's rolling daily budget (S2). On the agent path
            // this is the sum across all loop iterations; null/zero usage is a no-op.
            if (geminiResponse.Success)
            {
                await _usageBudgetService.RecordModelUsageAsync(
                    session.UserProfileId,
                    new UsageBudgetCharge
                    {
                        Tokens = geminiResponse.TokenUsage?.TotalTokens ?? 0,
                        CostMicroUsd = costEstimate?.TotalMicroUsd ?? 0
                    },
                    cancellationToken);
                usageSnapshot = await _usageBudgetService.GetDailyTokenUsageSnapshotAsync(session.UserProfileId, cancellationToken);
            }

            // Update session last activity

            session.LastActivityAt = DateTimeOffset.UtcNow;

            session.ExpiresAt = DateTimeOffset.UtcNow.AddHours(24);

            await _sessionRepository.UpdateAsync(session, cancellationToken);



            stopwatch.Stop();



            // Record metrics

            _metrics.RecordConversation();

            _metrics.RecordResponseLatency(stopwatch.Elapsed.TotalMilliseconds);



            // Publish ChatbotMessageReceivedEvent

            await _eventPublisher.PublishAsync(new ChatbotMessageReceivedEvent

            {

                MessageId = Guid.NewGuid(),

                Timestamp = DateTimeOffset.UtcNow,

                Source = "ChatbotService",

                CorrelationId = session.Id,

                SessionId = session.Id,

                UserProfileId = session.UserProfileId,

                Channel = session.Channel.ToString(),

                Language = detectedLanguage.ToString(),

                UserMessageContent = command.Content,

                AssistantResponseContent = formattedContent,

                ResponseLatencyMs = stopwatch.Elapsed.TotalMilliseconds,

                ReceivedAt = userMessage.CreatedAt

            }, cancellationToken);



            _logger.LogInformation("Processed message in session {SessionId} with language {Language} in {ElapsedMs}ms",

                session.Id, detectedLanguage, stopwatch.Elapsed.TotalMilliseconds);



            return new SendMessageResult
            {
                MessageId = messageId,
                Content = formattedContent,
                Role = MessageRole.Assistant,
                Language = detectedLanguage,
                SuggestedActions = suggestedActions,
                CreatedAt = createdAt,
                ThinkingSteps = thinkingSteps,
                ThoughtContent = geminiResponse.ThoughtContent,
                SessionId = session.Id,
                UsageSnapshot = usageSnapshot
            };


        }
        finally
        {
            try
            {
                await db.LockReleaseAsync(lockKey, lockValue);
            }
            finally
            {
                await DeleteStagedFilesAsync(stagedFileNames);
            }
        }
    }

    private async Task<GeminiAttachment> BuildCurrentTurnAttachmentAsync(
        AttachmentDto attachment,
        int attachmentNumber,
        ICollection<string> stagedFileNames,
        CancellationToken cancellationToken)
    {
        if (ShouldStageInlineAttachment(attachment, out var decodedBytes))
        {
            var stagedFile = await TryStageCurrentTurnAttachmentAsync(
                attachment,
                attachmentNumber,
                decodedBytes,
                cancellationToken);

            if (stagedFile is not null)
            {
                if (!string.IsNullOrWhiteSpace(stagedFile.Name))
                {
                    stagedFileNames.Add(stagedFile.Name);
                }

                return new GeminiAttachment
                {
                    ContentType = attachment.ContentType.ToString(),
                    Data = stagedFile.FileUri,
                    MimeType = string.IsNullOrWhiteSpace(stagedFile.MimeType)
                        ? attachment.MimeType
                        : stagedFile.MimeType
                };
            }

            throw new InvalidOperationException("Gemini file staging failed for an oversized attachment.");
        }

        return new GeminiAttachment
        {
            ContentType = attachment.ContentType.ToString(),
            Data = attachment.Data,
            MimeType = attachment.MimeType
        };
    }

    private async Task<ModelFileReference?> TryStageCurrentTurnAttachmentAsync(
        AttachmentDto attachment,
        int attachmentNumber,
        byte[] decodedBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _modelFileStagingService.StageFileAsync(
                new ModelFileStagingRequest
                {
                    FileName = BuildChatAttachmentFileName(attachment, attachmentNumber),
                    MimeType = attachment.MimeType,
                    Content = decodedBytes
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Gemini file staging failed for chat attachment {AttachmentNumber}.",
                attachmentNumber);
            return null;
        }
    }

    private async Task DeleteStagedFilesAsync(IReadOnlyCollection<string> stagedFileNames)
    {
        if (stagedFileNames.Count == 0)
        {
            return;
        }

        foreach (var fileName in stagedFileNames
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal))
        {
            try
            {
                await _modelFileStagingService.DeleteFileAsync(fileName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Gemini staged file cleanup failed for chat attachment {FileName}.",
                    fileName);
            }
        }
    }

    private bool ShouldStageInlineAttachment(AttachmentDto attachment, out byte[] decodedBytes)
    {
        decodedBytes = [];
        var base64Payload = NormalizeBase64Payload(attachment.Data);

        if (_fileApiInlineThresholdBytes <= 0 ||
            IsModelFetchedAttachmentReference(attachment.Data) ||
            !TryGetBase64DecodedLength(base64Payload, out var decodedLength) ||
            decodedLength < _fileApiInlineThresholdBytes)
        {
            return false;
        }

        try
        {
            decodedBytes = Convert.FromBase64String(base64Payload);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildChatAttachmentFileName(AttachmentDto attachment, int attachmentNumber)
    {
        var extension = attachment.MimeType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "audio/mpeg" => ".mp3",
            _ => string.Empty
        };

        return $"chat-attachment-{attachmentNumber}{extension}";
    }

    private static bool IsModelFetchedAttachmentReference(string data) =>
        data.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        data.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        data.StartsWith("gs://", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBase64Payload(string base64Data)
    {
        var payloadStart = base64Data.IndexOf(',', StringComparison.Ordinal);
        return payloadStart >= 0 ? base64Data[(payloadStart + 1)..] : base64Data;
    }

    private static bool TryGetBase64DecodedLength(string base64Payload, out long decodedLength)
    {
        var payload = new string(base64Payload.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (payload.Length == 0 || payload.Length % 4 != 0)
        {
            decodedLength = 0;
            return false;
        }

        var padding = payload.EndsWith("==", StringComparison.Ordinal)
            ? 2
            : payload.EndsWith("=", StringComparison.Ordinal) ? 1 : 0;
        decodedLength = (payload.Length / 4L * 3L) - padding;
        return decodedLength >= 0;
    }

    private async Task<GeminiResponse> SendGeminiMaybeStreamingAsync(
        GeminiRequest request,
        Func<string, Task>? onTextDelta,
        Func<string, Task>? onThoughtDelta,
        CancellationToken cancellationToken)
    {
        if (onTextDelta == null)
        {
            return await _geminiClient.SendMessageAsync(request, cancellationToken);
        }

        GeminiResponse? finalResponse = null;
        try
        {
            await foreach (var streamEvent in _geminiClient.StreamMessageAsync(request, cancellationToken))
            {
                if (streamEvent.Type.Equals("delta", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(streamEvent.Delta))
                {
                    await onTextDelta(streamEvent.Delta);
                }
                else if (streamEvent.Type.Equals("thought", StringComparison.OrdinalIgnoreCase) &&
                         !string.IsNullOrEmpty(streamEvent.Thought) && onThoughtDelta != null)
                {
                    await onThoughtDelta(streamEvent.Thought);
                }
                else if (streamEvent.Type.Equals("final", StringComparison.OrdinalIgnoreCase))
                {
                    finalResponse = streamEvent.Response;
                }
                else if (streamEvent.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    finalResponse = new GeminiResponse
                    {
                        Success = false,
                        ErrorMessage = streamEvent.ErrorMessage ?? "Gemini streaming failed"
                    };
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new GeminiResponse
            {
                Success = false,
                ErrorMessage = "Gemini streaming failed"
            };
        }

        return finalResponse ?? new GeminiResponse
        {
            Success = false,
            ErrorMessage = "Gemini streaming ended without a final response."
        };
    }

    private static string BuildSummariesContext(IEnumerable<ConversationSummary> summaries)
    {
        var contextParts = new List<string>();

        foreach (var summary in summaries)
        {
            try
            {
                var summaryDoc = JsonDocument.Parse(summary.StructuredSummary);
                var root = summaryDoc.RootElement;

                var summaryParts = new List<string>();

                // Extract topics
                if (root.TryGetProperty("topics", out var topics))
                {
                    var topicsList = topics.EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    if (topicsList.Count > 0)
                    {
                        summaryParts.Add($"Topics discussed: {string.Join(", ", topicsList)}");
                    }
                }

                // Extract decisions
                if (root.TryGetProperty("decisions", out var decisions))
                {
                    var decisionsList = decisions.EnumerateArray()
                        .Select(d => d.GetString())
                        .Where(d => !string.IsNullOrEmpty(d))
                        .ToList();

                    if (decisionsList.Count > 0)
                    {
                        summaryParts.Add($"Decisions: {string.Join(", ", decisionsList)}");
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
                        summaryParts.Add($"Preferences: {string.Join(", ", prefsList)}");
                    }
                }

                // Extract unresolved questions
                if (root.TryGetProperty("unresolvedQuestions", out var unresolvedQuestions))
                {
                    var questionsList = unresolvedQuestions.EnumerateArray()
                        .Select(q => q.GetString())
                        .Where(q => !string.IsNullOrEmpty(q))
                        .ToList();

                    if (questionsList.Count > 0)
                    {
                        summaryParts.Add($"Unresolved questions: {string.Join(", ", questionsList)}");
                    }
                }

                if (summaryParts.Count > 0)
                {
                    contextParts.Add(string.Join(". ", summaryParts));
                }
            }
            catch (JsonException)
            {
                // Skip invalid summaries
                continue;
            }
        }

        return contextParts.Count > 0 ? string.Join("\n", contextParts) : string.Empty;
    }

    private static GeminiMessage? BuildDynamicContextMessage(IEnumerable<string> contextSections)
    {
        var sections = contextSections
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .ToList();

        if (sections.Count == 0)
        {
            return null;
        }

        return new GeminiMessage
        {
            Role = "user",
            Content = "Context for this response. Use this as background; it is not a new customer request.\n\n" +
                string.Join("\n\n", sections)
        };
    }

    private static void InsertDynamicContextBeforeLatestUserTurn(
        List<GeminiMessage> messages,
        GeminiMessage dynamicContextMessage)
    {
        var latestUserMessageIndex = messages.FindLastIndex(message => message.Role == "user");
        if (latestUserMessageIndex < 0)
        {
            messages.Add(dynamicContextMessage);
            return;
        }

        messages.Insert(latestUserMessageIndex, dynamicContextMessage);
    }

    private static string GetCoreInstructionTopicKey(Channel channel)
    {
        return channel switch
        {
            Channel.Intranet => "intranet",
            Channel.QuoteEngine => "quote-engine",
            _ => "website"
        };
    }

    private static bool IsAgentToolChannel(Channel channel)
    {
        return channel is Channel.Intranet or Channel.QuoteEngine;
    }

    private static bool ShouldAttachAgentTools(
        Channel channel,
        string message,
        IReadOnlyCollection<AttachmentDto>? attachments)
    {
        if (!IsAgentToolChannel(channel))
        {
            return false;
        }

        if (attachments is { Count: > 0 })
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var messageLower = message.ToLowerInvariant();
        return ContainsAny(messageLower, AgentToolKeywords);
    }

    private static string GetToolProfile(Channel channel)
    {
        return channel switch
        {
            Channel.Intranet => "intranet",
            Channel.QuoteEngine => "quote-engine",
            _ => "website"
        };
    }

    private string? ResolveMediaResolution(IEnumerable<GeminiMessage> messages)
    {
        var mediaAttachments = messages
            .SelectMany(message => message.Attachments ?? [])
            .Where(IsMediaAttachment)
            .ToList();

        if (mediaAttachments.Count == 0)
        {
            return null;
        }

        return mediaAttachments
            .Select(ResolveMediaResolutionForAttachment)
            .OrderByDescending(GetMediaResolutionRank)
            .First();
    }

    private string ResolveMediaResolutionForAttachment(GeminiAttachment attachment)
    {
        if (IsVideoAttachment(attachment))
        {
            return _chatVideoMediaResolution;
        }

        if (IsPdfAttachment(attachment))
        {
            return _chatPdfMediaResolution;
        }

        return _chatImageMediaResolution;
    }

    private static bool IsMediaAttachment(GeminiAttachment attachment)
    {
        if (Enum.TryParse<ContentType>(attachment.ContentType, ignoreCase: true, out var contentType) &&
            contentType is ContentType.Image or ContentType.PDF or ContentType.Video)
        {
            return true;
        }

        return attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            attachment.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
            attachment.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVideoAttachment(GeminiAttachment attachment)
    {
        if (Enum.TryParse<ContentType>(attachment.ContentType, ignoreCase: true, out var contentType))
        {
            return contentType == ContentType.Video;
        }

        return attachment.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPdfAttachment(GeminiAttachment attachment)
    {
        if (Enum.TryParse<ContentType>(attachment.ContentType, ignoreCase: true, out var contentType))
        {
            return contentType == ContentType.PDF;
        }

        return attachment.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveConfiguredMediaResolution(
        string? configuredValue,
        string configurationKey,
        string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return defaultValue;
        }

        return configuredValue.Trim().ToUpperInvariant() switch
        {
            "LOW" or "MEDIA_RESOLUTION_LOW" => "MEDIA_RESOLUTION_LOW",
            "MEDIUM" or "MEDIA_RESOLUTION_MEDIUM" => "MEDIA_RESOLUTION_MEDIUM",
            "HIGH" or "MEDIA_RESOLUTION_HIGH" => "MEDIA_RESOLUTION_HIGH",
            "UNSPECIFIED" or "MEDIA_RESOLUTION_UNSPECIFIED" => "MEDIA_RESOLUTION_UNSPECIFIED",
            _ => throw new InvalidOperationException(
                $"Unsupported Gemini media resolution configured at '{configurationKey}'. " +
                "Use low, medium, high, unspecified, or the matching MEDIA_RESOLUTION_* enum value.")
        };
    }

    private static int GetMediaResolutionRank(string mediaResolution)
    {
        return mediaResolution switch
        {
            "MEDIA_RESOLUTION_LOW" => 1,
            "MEDIA_RESOLUTION_MEDIUM" or "MEDIA_RESOLUTION_UNSPECIFIED" => 2,
            "MEDIA_RESOLUTION_HIGH" => 3,
            _ => 0
        };
    }

    private static string? ResolveChatModelName(string? requestedModelName)
    {
        if (string.IsNullOrWhiteSpace(requestedModelName))
        {
            return null;
        }

        var normalizedModelName = requestedModelName.Trim();
        return AllowedChatModelOverrides.Contains(normalizedModelName) ? normalizedModelName : null;
    }

    private static string ResolveDefaultChatModelName(IConfiguration? configuration)
    {
        if (IsOpenAiCompatibleProvider(configuration?["Llm:Provider"]))
        {
            return FirstNonEmpty(
                configuration?["Llm:OpenAICompatible:ModelName"],
                configuration?["OpenAICompatible:ModelName"],
                configuration?["Gemini:MainModelName"])
            ?? "gemini-2.5-flash";
        }

        return FirstNonEmpty(
            configuration?["Gemini:MainModelName"],
            configuration?["Llm:OpenAICompatible:ModelName"],
            configuration?["OpenAICompatible:ModelName"])
        ?? "gemini-2.5-flash";
    }

    private static bool IsOpenAiCompatibleProvider(string? providerName) =>
        string.Equals(providerName?.Trim(), "openai-compatible", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static (string? ResponseMimeType, object? ResponseSchema) ResolveStructuredOutput(
        string? requestedResponseMimeType,
        object? requestedResponseSchema)
    {
        if (string.IsNullOrWhiteSpace(requestedResponseMimeType))
        {
            return (null, null);
        }

        if (!string.Equals(requestedResponseMimeType.Trim(), JsonResponseMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        if (requestedResponseSchema is null)
        {
            return (JsonResponseMimeType, null);
        }

        var schemaJson = JsonSerializer.Serialize(requestedResponseSchema);
        return schemaJson.Length <= MaxStructuredOutputSchemaJsonCharacters
            ? (JsonResponseMimeType, requestedResponseSchema)
            : (null, null);
    }

    private static object? BuildTokenUsageMetadata(GeminiTokenUsage? tokenUsage)
    {
        return tokenUsage is null
            ? null
            : new
            {
                promptTokens = tokenUsage.PromptTokens,
                cachedPromptTokens = tokenUsage.CachedPromptTokens,
                toolUsePromptTokens = tokenUsage.ToolUsePromptTokens,
                thoughtTokens = tokenUsage.ThoughtTokens,
                completionTokens = tokenUsage.CompletionTokens,
                totalTokens = tokenUsage.TotalTokens,
                promptTokenDetails = BuildModalityTokenDetails(tokenUsage.PromptTokenDetails),
                cachedTokenDetails = BuildModalityTokenDetails(tokenUsage.CachedTokenDetails),
                candidateTokenDetails = BuildModalityTokenDetails(tokenUsage.CandidateTokenDetails),
                toolUsePromptTokenDetails = BuildModalityTokenDetails(tokenUsage.ToolUsePromptTokenDetails)
            };
    }

    private static object? BuildGroundingMetadata(GeminiResponse response)
    {
        return response.GroundingWebSearchQueries.Count == 0
            ? null
            : new
            {
                webSearchQueries = response.GroundingWebSearchQueries.ToArray()
            };
    }

    private static object[] BuildModalityTokenDetails(IReadOnlyCollection<GeminiModalityTokenCount> details)
    {
        return details
            .Select(detail => new
            {
                modality = detail.Modality,
                tokenCount = detail.TokenCount
            })
            .ToArray();
    }

    private static object? BuildCostEstimateMetadata(GeminiCostEstimate? estimate)
    {
        return estimate is null
            ? null
            : new
            {
                modelName = estimate.ModelName,
                serviceTier = estimate.ServiceTier,
                pricingBasis = estimate.PricingBasis,
                uncachedPromptTokens = estimate.UncachedPromptTokens,
                cachedPromptTokens = estimate.CachedPromptTokens,
                toolUsePromptTokens = estimate.ToolUsePromptTokens,
                googleSearchGroundingPromptCount = estimate.GoogleSearchGroundingPromptCount,
                outputTokens = estimate.OutputTokens,
                uncachedPromptMicroUsd = estimate.UncachedPromptMicroUsd,
                cachedPromptMicroUsd = estimate.CachedPromptMicroUsd,
                toolUsePromptMicroUsd = estimate.ToolUsePromptMicroUsd,
                googleSearchGroundingMicroUsd = estimate.GoogleSearchGroundingMicroUsd,
                outputMicroUsd = estimate.OutputMicroUsd,
                totalMicroUsd = estimate.TotalMicroUsd
            };
    }

    private static int GetGoogleSearchGroundingPromptCount(GeminiResponse response) =>
        response.GroundingWebSearchQueries.Count > 0 ? 1 : 0;

    /// <summary>
    /// Detects whether the user message explicitly requires Google Search grounding.
    /// </summary>
    /// <param name="message">The user message.</param>
    /// <returns>True if web search should be triggered.</returns>
    private static bool ShouldTriggerWebSearch(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var groundedTopicKeywords = new[]
        {
            "astm", "iso", "din", "jis", "spec", "specification",
            "standard", "properties", "technical", "datasheet",
            "material properties", "chemical composition"
        };

        var messageLower = message.ToLowerInvariant();
        if (!ContainsAny(messageLower, groundedTopicKeywords))
        {
            return false;
        }

        var freshnessKeywords = new[]
        {
            "latest", "current", "currently", "recent", "recently",
            "updated", "newest", "new version", "changed", "still valid",
            "today", "this week", "this month", "this year"
        };
        var sourceLookupKeywords = new[]
        {
            "official", "source", "citation", "cite", "reference",
            "link", "url", "find", "lookup", "look up", "online", "web"
        };

        return ContainsAny(messageLower, freshnessKeywords) || ContainsAny(messageLower, sourceLookupKeywords);
    }

    private static bool ContainsAny(string value, IEnumerable<string> keywords) =>
        keywords.Any(keyword => value.Contains(keyword, StringComparison.Ordinal));

    private static bool ShouldEnableUrlContext(
        string message,
        bool urlContextEnabled,
        int maxUrlsPerRequest,
        bool allowModelThoughts)
    {
        if (!urlContextEnabled ||
            allowModelThoughts ||
            string.IsNullOrWhiteSpace(message) ||
            !HasUrlAnalysisIntent(message))
        {
            return false;
        }

        var publicUrlCount = 0;
        foreach (var token in message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = TrimUrlCandidate(token);
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (!IsSupportedUrlContextUri(uri))
            {
                return false;
            }

            publicUrlCount++;
            if (publicUrlCount > maxUrlsPerRequest)
            {
                return false;
            }
        }

        return publicUrlCount > 0;
    }

    private static bool HasUrlAnalysisIntent(string message)
    {
        var messageLower = message.ToLowerInvariant();
        var urlAnalysisKeywords = new[]
        {
            "summarize", "summarise", "analyze", "analyse", "compare",
            "extract", "read", "review", "based on", "using http",
            "from http", "at http", "what does", "what do", "tell me"
        };

        return ContainsAny(messageLower, urlAnalysisKeywords);
    }

    private static string TrimUrlCandidate(string token) =>
        token.TrimStart('(', '[', '{', '<', '"', '\'')
            .TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'');

    private static bool IsSupportedUrlContextUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.IsLoopback ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IPAddress.TryParse(uri.Host, out var address) || !IsPrivateAddress(address);
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return IPAddress.IsLoopback(address) ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal ||
                (bytes.Length > 0 && (bytes[0] & 0xfe) == 0xfc);
        }

        return false;
    }

    private Language ResolveMessageLanguage(string? language, string content)
    {
        return language?.ToLowerInvariant() switch
        {
            "th" => Language.Thai,
            "en" => Language.English,
            _ => _languageDetectionService.DetectLanguage(content)
        };
    }

    private static long ResolveMaxFileSizeBytes(IConfiguration? configuration, string key, int defaultSizeMb)
    {
        var sizeMb = configuration?.GetValue<int?>(key) ?? defaultSizeMb;
        return Math.Max(1, sizeMb) * 1024L * 1024L;
    }

    /// <summary>
    /// Detects operation intent from user message for internal agents.
    /// </summary>
    /// <param name="content">The user message content.</param>
    /// <returns>Operation intent with type and parameters, or null if no intent detected.</returns>
    private static OperationIntent? DetectOperationIntent(string content)
    {
        var contentLower = content.ToLowerInvariant();

        // Detect quotation query
        if (contentLower.Contains("quotation") || contentLower.Contains("quote") || contentLower.Contains("ใบเสนอราคา"))
        {
            var quotationIdMatch = System.Text.RegularExpressions.Regex.Match(content, @"Q-\d{4}-\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (quotationIdMatch.Success)
            {
                if (contentLower.Contains("reminder") || contentLower.Contains("ส่งแจ้งเตือน"))
                {
                    return new OperationIntent
                    {
                        OperationType = "SendReminder",
                        Parameters = new Dictionary<string, object> { { "quotationId", quotationIdMatch.Value } }
                    };
                }

                return new OperationIntent
                {
                    OperationType = "QuotationQuery",
                    Parameters = new Dictionary<string, object> { { "quotationId", quotationIdMatch.Value } }
                };
            }
        }

        // Detect order status query
        if (contentLower.Contains("order") || contentLower.Contains("คำสั่งซื้อ"))
        {
            var orderIdMatch = System.Text.RegularExpressions.Regex.Match(content, @"O-\d{4}-\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (orderIdMatch.Success)
            {
                return new OperationIntent
                {
                    OperationType = "OrderStatusQuery",
                    Parameters = new Dictionary<string, object> { { "orderId", orderIdMatch.Value } }
                };
            }
        }

        // Detect CRM update
        if (contentLower.Contains("update") && (contentLower.Contains("customer") || contentLower.Contains("crm") || contentLower.Contains("ลูกค้า")))
        {
            return new OperationIntent
            {
                OperationType = "CRMUpdate",
                Parameters = new Dictionary<string, object>()
            };
        }

        return null;
    }
}

/// <summary>
/// Represents detected operation intent from user message.
/// </summary>
internal class OperationIntent
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Result of sending a message.
/// </summary>
public class SendMessageResult
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message role.
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// Gets or sets the language.
    /// </summary>
    public Language Language { get; set; }

    /// <summary>
    /// Gets or sets the suggested actions.
    /// </summary>
    public List<SuggestedActionDto> SuggestedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the thinking steps from agent processing.
    /// </summary>
    public List<Models.ThinkingStep> ThinkingSteps { get; set; } = new();

    /// <summary>
    /// Gets or sets the accumulated model thought/reasoning content.
    /// </summary>
    public string? ThoughtContent { get; set; }

    /// <summary>
    /// Gets or sets the session ID for SignalR correlation.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the current daily token usage snapshot.
    /// </summary>
    public UsageBudgetSnapshot? UsageSnapshot { get; set; }
}
