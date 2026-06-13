using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Validators;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Domain.Events;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
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
    private readonly IGeminiClient _geminiClient;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SendMessageCommandHandler"/> class.
    /// </summary>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="messageRepository">The message repository.</param>
    /// <param name="userProfileRepository">The user profile repository.</param>
    /// <param name="knowledgeBaseRepository">The knowledge base repository.</param>
    /// <param name="summaryService">The conversation summary service.</param>
    /// <param name="rateLimitService">The rate limit service.</param>
    /// <param name="geminiClient">The Gemini API client.</param>
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
    public SendMessageCommandHandler(
        IConversationSessionRepository sessionRepository,
        IMessageRepository messageRepository,
        IUserProfileRepository userProfileRepository,
        IKnowledgeBaseRepository knowledgeBaseRepository,
        IConversationSummaryService summaryService,
        IRateLimitService rateLimitService,
        IGeminiClient geminiClient,
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
        ILogger<SendMessageCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _userProfileRepository = userProfileRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _summaryService = summaryService;
        _rateLimitService = rateLimitService;
        _geminiClient = geminiClient;
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

        // Try to acquire lock for 30 seconds (Gemini timeout max)
        if (!await db.LockTakeAsync(lockKey, lockValue, TimeSpan.FromSeconds(35)))
        {
            _logger.LogWarning("Could not acquire lock for session {SessionId}. System busy.", command.SessionId);
            throw new InvalidOperationException("System is busy processing your previous message. Please wait a moment.");
        }

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

            // Validate attachment sizes
            if (command.Attachments != null && command.Attachments.Count > 0)
            {
                foreach (var attachment in command.Attachments)
                {
                    const long maxImageSize = 10 * 1024 * 1024; // 10 MB
                    const long maxPdfSize = 20 * 1024 * 1024; // 20 MB
                    const long maxVideoSize = 50 * 1024 * 1024; // 50 MB

                    var maxSize = attachment.ContentType switch
                    {
                        ContentType.Image => maxImageSize,
                        ContentType.PDF => maxPdfSize,
                        ContentType.Video => maxVideoSize,
                        ContentType.Audio => maxVideoSize,
                        _ => maxImageSize
                    };

                    if (attachment.SizeBytes > maxSize)
                    {
                        var maxSizeMB = maxSize / (1024 * 1024);
                        throw new InvalidOperationException($"Attachment size exceeds the maximum allowed size of {maxSizeMB}MB for {attachment.ContentType} files.");
                    }
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

            // Save user message
            var userMessage = new Message
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = MessageRole.User,
                Content = command.Content,
                ContentType = ContentType.Text,
                CreatedAt = DateTimeOffset.UtcNow
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

            // 1. Classify Intent
            var classification = await _intentClassificationService.ClassifyIntentAsync(command.Content, cancellationToken);
            _metrics.RecordIntentClassification(classification.Intent, classification.Confidence);

            var topicKeys = new List<string>();
            if (classification.Intent != "General" && classification.Confidence > 0.7)
            {
                topicKeys.Add(classification.Intent);
                _metrics.RecordContextInjection("Topic", classification.Intent);
            }
            if (classification.AdditionalTopics != null)
            {
                foreach (var topic in classification.AdditionalTopics)
                {
                    topicKeys.Add(topic);
                    _metrics.RecordContextInjection("Topic", topic);
                }
            }

            // 2. Get merged instructions using the channel-specific core prompt profile.
            var coreInstructionTopicKey = GetCoreInstructionTopicKey(session.Channel);
            var systemInstructionText = await _systemInstructionService.GetMergedInstructionsAsync(
                topicKeys,
                coreInstructionTopicKey,
                cancellationToken);

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
                    systemInstructionText += "\n\n## RELEVANT FACTS AND CONTEXT\n";
                    foreach (var fact in facts)
                    {
                        systemInstructionText += $"- {fact.Content}\n";
                        injectedKnowledgeIds.Add(fact.Id);
                    }
                }
            }

            // Add summaries context to system instruction if available
            if (!string.IsNullOrEmpty(summariesContext))
            {
                systemInstructionText += $"\n\nPrevious conversation context:\n{summariesContext}";
            }

            // Get system instruction for business constraint validation (Core only for now)
            var coreInstruction = await _systemInstructionService.GetActiveInstructionAsync(coreInstructionTopicKey, cancellationToken);

            // Check if Gemini built-in search should be triggered
            bool enableGeminiSearch = false;
            if (coreInstruction != null &&
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

            // Build Gemini request with built-in search if enabled
            var geminiRequest = new GeminiRequest
            {
                SystemInstruction = systemInstructionText,
                Messages = conversationHistory
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new GeminiMessage
                    {
                        Role = m.Role == MessageRole.User ? "user" : "assistant",
                        Content = m.Content
                    })
                    .ToList(),
                TimeoutSeconds = enableGeminiSearch ? 30 : 10,
                ResponseMimeType = command.ResponseMimeType,
                ResponseSchema = command.ResponseSchema,
                EnableWebSearch = enableGeminiSearch
            };

            // Use the agent loop only for channels with scoped, allowlisted tool profiles.
            GeminiResponse geminiResponse;
            var thinkingSteps = new List<Models.ThinkingStep>();

            if (IsAgentToolChannel(session.Channel) && string.IsNullOrEmpty(command.ResponseMimeType))
            {
                var tools = _toolExecutor.GetToolDeclarations(GetToolProfile(session.Channel));
                if (tools.Count > 0)
                {
                    geminiRequest.Tools = tools;
                    geminiRequest.ToolConfig = new GeminiFunctionCallingConfig { Mode = "AUTO" };
                    geminiRequest.TimeoutSeconds = 30;

                    var agentResult = await _agentChatHandler.ExecuteAsync(
                        geminiRequest,
                        command.ThinkingStepCallback,
                        command.UserToken,
                        command.QuoteAgentContextToken,
                        cancellationToken);

                    thinkingSteps = agentResult.ThinkingSteps;
                    geminiResponse = new GeminiResponse
                    {
                        Success = agentResult.Success,
                        Content = agentResult.Content,
                        ErrorMessage = agentResult.ErrorMessage,
                        TokenUsage = agentResult.TokenUsage
                    };
                }
                else
                {
                    geminiResponse = await _geminiClient.SendMessageAsync(geminiRequest, cancellationToken);
                }
            }
            else
            {
                geminiResponse = await _geminiClient.SendMessageAsync(geminiRequest, cancellationToken);
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



            if (geminiResponse.Success)

            {

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

                        injectedKnowledgeIds = injectedKnowledgeIds

                    })

                };



                await _messageRepository.CreateAsync(assistantMessage, cancellationToken);

                messageId = assistantMessage.Id;

                createdAt = assistantMessage.CreatedAt;

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
                SessionId = session.Id
            };


        }
        finally
        {
            await db.LockReleaseAsync(lockKey, lockValue);
        }
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

    private static string GetToolProfile(Channel channel)
    {
        return channel switch
        {
            Channel.Intranet => "intranet",
            Channel.QuoteEngine => "quote-engine",
            _ => "website"
        };
    }


    /// <summary>
    /// Detects whether the user message requires web search based on keywords.
    /// </summary>
    /// <param name="message">The user message.</param>
    /// <returns>True if web search should be triggered.</returns>
    private static bool ShouldTriggerWebSearch(string message)
    {
        var searchKeywords = new[]
        {
            "astm", "iso", "din", "jis", "spec", "specification",
            "standard", "properties", "technical", "datasheet",
            "material properties", "chemical composition"
        };

        var messageLower = message.ToLowerInvariant();
        return searchKeywords.Any(keyword => messageLower.Contains(keyword));
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
    /// Gets or sets the session ID for SignalR correlation.
    /// </summary>
    public Guid? SessionId { get; set; }
}
