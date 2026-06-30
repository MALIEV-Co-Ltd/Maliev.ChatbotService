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

    private static async Task<HandlerResult> SendWebsiteMessageAsync(List<AttachmentDto>? attachments = null)
    {
        var sessionId = Guid.NewGuid();
        var userProfileId = Guid.NewGuid();
        GeminiRequest? capturedRequest = null;
        var sessionRepository = new Mock<IConversationSessionRepository>();
        sessionRepository
            .Setup(item => item.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationSession
            {
                Id = sessionId,
                UserProfileId = userProfileId,
                Channel = Channel.Website,
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
            .ReturnsAsync((Message message, CancellationToken _) => message);
        messageRepository
            .Setup(item => item.GetRecentBySessionIdAsync(sessionId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Message
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Role = MessageRole.User,
                    Content = "What materials can you print?",
                    ContentType = ContentType.Text,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]);

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
        var summaryService = new Mock<IConversationSummaryService>();
        summaryService
            .Setup(item => item.GetRecentSummariesAsync(userProfileId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

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
                TokenUsage = new GeminiTokenUsage { TotalTokens = 25 }
            });

        var systemInstructionService = new Mock<ISystemInstructionService>();
        systemInstructionService
            .Setup(item => item.GetMergedInstructionsAsync(
                It.IsAny<IEnumerable<string>>(),
                "website",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("You are MALIEV's customer assistant.");
        systemInstructionService
            .Setup(item => item.GetActiveInstructionAsync("website", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SystemInstruction { EnableWebSearch = false });

        var intentClassificationService = new Mock<IIntentClassificationService>();
        intentClassificationService
            .Setup(item => item.ClassifyIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentClassificationResult { Intent = "General", Confidence = 0.99 });

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
            Language = "en",
            Attachments = attachments
        });

        return new HandlerResult(capturedRequest, toolExecutor);
    }

    private sealed record HandlerResult(GeminiRequest? CapturedRequest, Mock<IToolExecutorService> ToolExecutor);
}
