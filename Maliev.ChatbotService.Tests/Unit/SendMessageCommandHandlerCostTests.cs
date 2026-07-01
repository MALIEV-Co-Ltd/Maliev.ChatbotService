using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Validators;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using System.Text.Json;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class SendMessageCommandHandlerCostTests
{
    [Fact]
    public async Task HandleAsync_WebsiteCustomerMessage_DisablesThinkingToAvoidDefaultReasoningCost()
    {
        var result = await SendWebsiteMessageAsync();

        Assert.NotNull(result.CapturedRequest);
        Assert.False(result.CapturedRequest!.IncludeThoughts);
        Assert.Equal(0, result.CapturedRequest.ThinkingBudget);
        result.ToolExecutor.Verify(item => item.GetToolDeclarations(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WebsiteCustomerMessage_BoundsGeneratedOutputTokens()
    {
        var result = await SendWebsiteMessageAsync();

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal(2048, result.CapturedRequest!.MaxTokens);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedModelOverride_UsesConfiguredDefaultModel()
    {
        var result = await SendWebsiteMessageAsync(modelName: "gemini-2.5-pro");

        Assert.NotNull(result.CapturedRequest);
        Assert.Null(result.CapturedRequest!.ModelName);
    }

    [Fact]
    public async Task HandleAsync_FlashLiteModelOverride_PreservesAllowedUtilityModel()
    {
        var result = await SendWebsiteMessageAsync(modelName: "gemini-2.5-flash-lite");

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal("gemini-2.5-flash-lite", result.CapturedRequest!.ModelName);
    }

    [Fact]
    public async Task HandleAsync_SupportedStructuredJsonSchema_PreservesBoundedSchema()
    {
        var responseSchema = new
        {
            type = "object",
            properties = new
            {
                clean_text = new { type = "string" }
            },
            required = new[] { "clean_text" }
        };

        var result = await SendWebsiteMessageAsync(
            responseMimeType: "application/json",
            responseSchema: responseSchema);

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal("application/json", result.CapturedRequest!.ResponseMimeType);
        Assert.Same(responseSchema, result.CapturedRequest.ResponseSchema);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedStructuredOutputMimeType_DoesNotForwardStructuredOutputConfig()
    {
        var result = await SendWebsiteMessageAsync(
            responseMimeType: "text/plain",
            responseSchema: new { type = "object" });

        Assert.NotNull(result.CapturedRequest);
        Assert.Null(result.CapturedRequest!.ResponseMimeType);
        Assert.Null(result.CapturedRequest.ResponseSchema);
    }

    [Fact]
    public async Task HandleAsync_OversizedStructuredOutputSchema_DoesNotForwardStructuredOutputConfig()
    {
        var result = await SendWebsiteMessageAsync(
            responseMimeType: "application/json",
            responseSchema: new
            {
                type = "object",
                description = new string('x', 17_000)
            });

        Assert.NotNull(result.CapturedRequest);
        Assert.Null(result.CapturedRequest!.ResponseMimeType);
        Assert.Null(result.CapturedRequest.ResponseSchema);
    }

    [Fact]
    public async Task HandleAsync_WebsiteCustomerMessage_PreflightsTextOnlyPromptTokens()
    {
        var result = await SendWebsiteMessageAsync();

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal(30000, result.CapturedRequest!.MaxPromptTokens);
    }

    [Fact]
    public async Task HandleAsync_WebsiteCustomerMessage_SkipsUnusedIntentClassification()
    {
        var result = await SendWebsiteMessageAsync();

        result.IntentClassificationService.Verify(
            item => item.ClassifyIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IntranetMessage_ClassifiesIntentForDomainTopicInjection()
    {
        var result = await SendWebsiteMessageAsync(channel: Channel.Intranet);

        result.IntentClassificationService.Verify(
            item => item.ClassifyIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_IntranetToolMessage_BoundsThinkingBudgetWhileIncludingThoughts()
    {
        var result = await SendWebsiteMessageAsync(
            channel: Channel.Intranet,
            toolDeclarations:
            [
                new GeminiToolDeclaration
                {
                    FunctionDeclarations =
                    [
                        new GeminiFunctionDeclaration
                        {
                            Name = "lookup_customer",
                            Description = "Looks up a customer record.",
                            Parameters = new
                            {
                                type = "object",
                                properties = new { customer_id = new { type = "string" } },
                                required = new[] { "customer_id" }
                            }
                        }
                    ]
                }
            ]);

        Assert.NotNull(result.CapturedRequest);
        Assert.True(result.CapturedRequest!.IncludeThoughts);
        Assert.Equal(1024, result.CapturedRequest.ThinkingBudget);
    }

    [Fact]
    public async Task HandleAsync_WebsiteMediaAttachment_UsesMediumMediaResolution()
    {
        var result = await SendWebsiteMessageAsync([
            new AttachmentDto
            {
                ContentType = ContentType.Image,
                Data = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB",
                MimeType = "image/png",
                SizeBytes = 1024
            }
        ]);

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal("MEDIA_RESOLUTION_MEDIUM", result.CapturedRequest!.MediaResolution);
    }

    [Fact]
    public async Task HandleAsync_WebsiteMediaAttachment_PreflightsPromptTokenCost()
    {
        var result = await SendWebsiteMessageAsync([
            new AttachmentDto
            {
                ContentType = ContentType.PDF,
                Data = "JVBERi0xLjQKJcTl8uXrp",
                MimeType = "application/pdf",
                SizeBytes = 1024
            }
        ]);

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal(30000, result.CapturedRequest!.MaxPromptTokens);
    }

    [Fact]
    public async Task HandleAsync_GeminiTokenUsage_PersistsCostBreakdownInAssistantMetadata()
    {
        var result = await SendWebsiteMessageAsync(tokenUsage: new GeminiTokenUsage
        {
            PromptTokens = 100,
            CachedPromptTokens = 35,
            ToolUsePromptTokens = 12,
            ThoughtTokens = 0,
            CompletionTokens = 25,
            TotalTokens = 137
        });

        var assistantMessage = Assert.Single(result.CreatedMessages, message => message.Role == MessageRole.Assistant);
        Assert.NotNull(assistantMessage.MetadataJson);
        using var metadata = JsonDocument.Parse(assistantMessage.MetadataJson!);
        var tokenUsage = metadata.RootElement.GetProperty("tokenUsage");
        Assert.Equal(100, tokenUsage.GetProperty("promptTokens").GetInt32());
        Assert.Equal(35, tokenUsage.GetProperty("cachedPromptTokens").GetInt32());
        Assert.Equal(12, tokenUsage.GetProperty("toolUsePromptTokens").GetInt32());
        Assert.Equal(0, tokenUsage.GetProperty("thoughtTokens").GetInt32());
        Assert.Equal(25, tokenUsage.GetProperty("completionTokens").GetInt32());
        Assert.Equal(137, tokenUsage.GetProperty("totalTokens").GetInt32());
    }

    [Fact]
    public async Task HandleAsync_DynamicContext_DoesNotMutateSystemInstructionForGeminiCaching()
    {
        var result = await SendWebsiteMessageAsync(
            summaries:
            [
                new ConversationSummary
                {
                    StructuredSummary = """
                        {
                          "topics": ["nylon PA12 quote"],
                          "decisions": ["Use white PA12 for the bracket"],
                          "preferences": ["Prefers metric dimensions"],
                          "unresolvedQuestions": ["Confirm lead time"]
                        }
                        """
                }
            ]);

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal("You are MALIEV's customer assistant.", result.CapturedRequest!.SystemInstruction);
        Assert.Equal("user", result.CapturedRequest.Messages[0].Role);
        Assert.Contains("Previous conversation context:", result.CapturedRequest.Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("Topics discussed: nylon PA12 quote", result.CapturedRequest.Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("Decisions: Use white PA12 for the bracket", result.CapturedRequest.Messages[0].Content, StringComparison.Ordinal);
        Assert.Equal("What materials can you print?", result.CapturedRequest.Messages[^1].Content);
    }

    [Fact]
    public async Task HandleAsync_DynamicContext_KeepsConversationHistoryPrefixForImplicitCaching()
    {
        var result = await SendWebsiteMessageAsync(
            conversationHistory:
            [
                new Message
                {
                    Id = Guid.NewGuid(),
                    SessionId = Guid.NewGuid(),
                    Role = MessageRole.User,
                    Content = "Earlier we discussed a nylon PA12 bracket.",
                    ContentType = ContentType.Text,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SessionId = Guid.NewGuid(),
                    Role = MessageRole.Assistant,
                    Content = "I can help quote that bracket.",
                    ContentType = ContentType.Text,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SessionId = Guid.NewGuid(),
                    Role = MessageRole.User,
                    Content = "What materials can you print?",
                    ContentType = ContentType.Text,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
                }
            ],
            summaries:
            [
                new ConversationSummary
                {
                    StructuredSummary = """
                        {
                          "topics": ["nylon PA12 quote"]
                        }
                        """
                }
            ]);

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal("Earlier we discussed a nylon PA12 bracket.", result.CapturedRequest!.Messages[0].Content);
        Assert.Equal("I can help quote that bracket.", result.CapturedRequest.Messages[1].Content);
        Assert.Contains("Previous conversation context:", result.CapturedRequest.Messages[2].Content, StringComparison.Ordinal);
        Assert.Equal("What materials can you print?", result.CapturedRequest.Messages[^1].Content);
    }

    [Fact]
    public async Task HandleAsync_IntranetKnowledgeContext_DoesNotMutateSystemInstructionForGeminiCaching()
    {
        var result = await SendWebsiteMessageAsync(
            channel: Channel.Intranet,
            classification: new IntentClassificationResult { Intent = "Inventory", Confidence = 0.95 },
            knowledgeFacts:
            [
                new KnowledgeBase
                {
                    TopicKey = "Inventory",
                    Content = "Use live inventory tools before promising material availability."
                }
            ]);

        Assert.NotNull(result.CapturedRequest);
        Assert.Equal("You are MALIEV's customer assistant.", result.CapturedRequest!.SystemInstruction);
        Assert.DoesNotContain("RELEVANT FACTS", result.CapturedRequest.SystemInstruction, StringComparison.Ordinal);
        Assert.Equal("user", result.CapturedRequest.Messages[0].Role);
        Assert.Contains("RELEVANT FACTS AND CONTEXT", result.CapturedRequest.Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("Use live inventory tools before promising material availability.", result.CapturedRequest.Messages[0].Content, StringComparison.Ordinal);
        Assert.Equal("What materials can you print?", result.CapturedRequest.Messages[^1].Content);
    }

    private static async Task<HandlerResult> SendWebsiteMessageAsync(
        List<AttachmentDto>? attachments = null,
        GeminiTokenUsage? tokenUsage = null,
        Channel channel = Channel.Website,
        IEnumerable<ConversationSummary>? summaries = null,
        IntentClassificationResult? classification = null,
        IReadOnlyList<KnowledgeBase>? knowledgeFacts = null,
        IReadOnlyList<Message>? conversationHistory = null,
        string? modelName = null,
        string? responseMimeType = null,
        object? responseSchema = null,
        List<GeminiToolDeclaration>? toolDeclarations = null)
    {
        var sessionId = Guid.NewGuid();
        var userProfileId = Guid.NewGuid();
        GeminiRequest? capturedRequest = null;
        var createdMessages = new List<Message>();
        var sessionRepository = new Mock<IConversationSessionRepository>();
        sessionRepository
            .Setup(item => item.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationSession
            {
                Id = sessionId,
                UserProfileId = userProfileId,
                Channel = channel,
                Status = SessionStatus.Active,
                Language = Language.English,
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });
        sessionRepository
            .Setup(item => item.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var messageRepository = new Mock<IMessageRepository>();
        messageRepository
            .Setup(item => item.CreateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => createdMessages.Add(message))
            .ReturnsAsync((Message message, CancellationToken _) => message);
        messageRepository
            .Setup(item => item.GetRecentBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((conversationHistory ?? [
                    new Message
                    {
                        Id = Guid.NewGuid(),
                        SessionId = sessionId,
                        Role = MessageRole.User,
                        Content = "What materials can you print?",
                        ContentType = ContentType.Text,
                        CreatedAt = DateTimeOffset.UtcNow
                    }
                ]).ToList());

        var userProfileRepository = new Mock<IUserProfileRepository>();
        userProfileRepository
            .Setup(item => item.GetByIdAsync(userProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile
            {
                Id = userProfileId,
                Role = UserRole.Customer,
                CreatedAt = DateTimeOffset.UtcNow,
                LastActiveAt = DateTimeOffset.UtcNow
            });

        var knowledgeBaseRepository = new Mock<IKnowledgeBaseRepository>();
        knowledgeBaseRepository
            .Setup(item => item.GetByTopicAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string topic, CancellationToken _) =>
                knowledgeFacts?.Where(fact => fact.TopicKey == topic).ToList() ?? []);

        var summaryService = new Mock<IConversationSummaryService>();
        summaryService
            .Setup(item => item.GetRecentSummariesAsync(userProfileId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries ?? []);

        var rateLimitService = new Mock<IRateLimitService>();
        rateLimitService
            .Setup(item => item.IncrementMessageCountAsync(userProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var usageBudgetService = new Mock<IUsageBudgetService>();
        usageBudgetService
            .Setup(item => item.GetDailyTokenUsageSnapshotAsync(userProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageBudgetSnapshot());
        usageBudgetService
            .Setup(item => item.RecordTokenUsageAsync(userProfileId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "We can print PLA, PETG, ABS, ASA, nylon, and engineering materials.",
                TokenUsage = tokenUsage ?? new GeminiTokenUsage { TotalTokens = 25 }
            });

        var systemInstructionService = new Mock<ISystemInstructionService>();
        systemInstructionService
            .Setup(item => item.GetMergedInstructionsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("You are MALIEV's customer assistant.");
        systemInstructionService
            .Setup(item => item.GetActiveInstructionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemInstruction { EnableWebSearch = false });

        var intentClassificationService = new Mock<IIntentClassificationService>();
        intentClassificationService
            .Setup(item => item.ClassifyIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(classification ?? new IntentClassificationResult { Intent = "General", Confidence = 0.99 });

        var languageDetectionService = new Mock<ILanguageDetectionService>();
        var responseFormatterService = new Mock<IResponseFormatterService>();
        responseFormatterService
            .Setup(item => item.FormatResponse(It.IsAny<string>(), Language.English))
            .Returns((string content, Language _) => (content, []));

        var operationLogRepository = new Mock<IOperationLogRepository>();
        var metrics = new Mock<IConversationMetrics>();
        var eventPublisher = new Mock<IEventPublisher>();
        eventPublisher
            .Setup(item => item.PublishAsync(It.IsAny<ChatbotMessageReceivedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var searchDomainLogRepository = new Mock<ISearchDomainLogRepository>();
        var webSearchService = new Mock<IWebSearchService>();
        var toolExecutor = new Mock<IToolExecutorService>();
        toolExecutor
            .Setup(item => item.GetToolDeclarations(It.IsAny<string>()))
            .Returns(toolDeclarations ?? new List<GeminiToolDeclaration>());
        var database = new Mock<IDatabase>();
        database
            .Setup(item => item.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(item => item.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        var handler = new SendMessageCommandHandler(
            sessionRepository.Object,
            messageRepository.Object,
            userProfileRepository.Object,
            knowledgeBaseRepository.Object,
            summaryService.Object,
            rateLimitService.Object,
            usageBudgetService.Object,
            geminiClient.Object,
            systemInstructionService.Object,
            intentClassificationService.Object,
            languageDetectionService.Object,
            responseFormatterService.Object,
            operationExecutionService: null,
            new BusinessConstraintValidator(Mock.Of<ILogger<BusinessConstraintValidator>>()),
            operationLogRepository.Object,
            metrics.Object,
            eventPublisher.Object,
            searchDomainLogRepository.Object,
            webSearchService.Object,
            new AgentChatHandler(geminiClient.Object, toolExecutor.Object, Mock.Of<ILogger<AgentChatHandler>>()),
            toolExecutor.Object,
            redis.Object,
            Mock.Of<ILogger<SendMessageCommandHandler>>());

        await handler.HandleAsync(new SendMessageCommand
        {
            SessionId = sessionId,
            Content = attachments is { Count: > 0 }
                ? "What can you tell from this attachment?"
                : "What materials can you print?",
            ModelName = modelName,
            Language = "en",
            Attachments = attachments,
            ResponseMimeType = responseMimeType,
            ResponseSchema = responseSchema
        });

        return new HandlerResult(capturedRequest, createdMessages, toolExecutor, intentClassificationService);
    }

    private sealed record HandlerResult(
        GeminiRequest? CapturedRequest,
        IReadOnlyList<Message> CreatedMessages,
        Mock<IToolExecutorService> ToolExecutor,
        Mock<IIntentClassificationService> IntentClassificationService);
}
