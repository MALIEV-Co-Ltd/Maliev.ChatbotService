using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class ExtractPreferencesServiceTests
{
    [Fact]
    public async Task ExtractPreferencesAsync_WithEmptyMessage_ReturnsEmptyList()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithWhitespaceMessage_ReturnsEmptyList()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("   ");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithMaterialPreference_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("I prefer to use aluminum material");

        Assert.NotEmpty(result);
        var materialPref = result.FirstOrDefault(r => r.Key == "MaterialPreference");
        Assert.NotNull(materialPref);
        Assert.Equal(0.95, materialPref.Confidence);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithMaterialLike_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("I like using steel material");

        Assert.NotEmpty(result);
        var materialPref = result.FirstOrDefault(r => r.Key == "MaterialPreference");
        Assert.NotNull(materialPref);
        Assert.Equal(0.90, materialPref.Confidence);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithMaterialAlways_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("I always go with aluminum for my parts");

        Assert.NotEmpty(result);
        var materialPref = result.FirstOrDefault(r => r.Key == "MaterialPreference");
        Assert.NotNull(materialPref);
        Assert.Equal(0.95, materialPref.Confidence);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithMaterialUsually_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("I usually use plastic material for prototypes");

        Assert.NotEmpty(result);
        var materialPref = result.FirstOrDefault(r => r.Key == "MaterialPreference");
        Assert.NotNull(materialPref);
        Assert.Equal(0.85, materialPref.Confidence);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithProcessPreference_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("My preference is CNC machining process");

        Assert.NotEmpty(result);
        var processPref = result.FirstOrDefault(r => r.Key == "ProcessPreference");
        Assert.NotNull(processPref);
        Assert.Equal(0.90, processPref.Confidence);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithQualityStandard_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("We require ISO 9001 certification");

        Assert.NotEmpty(result);
        var qualityPref = result.FirstOrDefault(r => r.Key == "QualityStandard");
        Assert.NotNull(qualityPref);
        Assert.Equal(0.92, qualityPref.Confidence);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithDeliveryPreference_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("I need it delivered in 5 days");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithQuantityPreference_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync("I prefer ordering 100 pieces");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExtractPreferencesAsync_WithMultiplePreferences_ExtractsAll()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);

        var result = await service.ExtractPreferencesAsync(
            "I prefer to use aluminum and my preference is CNC process. We require ISO 9001 and need delivery in 3 days");

        Assert.NotEmpty(result);
        Assert.True(result.Count >= 3);
    }
}

public class ConversationSummaryServiceTests
{
    [Fact]
    public async Task GenerateSummaryAsync_WithNoSession_ThrowsException()
    {
        var mockSummaryRepo = new Mock<IConversationSummaryRepository>();
        var mockSessionRepo = new Mock<IConversationSessionRepository>();
        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockGeminiClient = new Mock<IGeminiClient>();
        var mockLogger = new Mock<ILogger<ConversationSummaryService>>();

        mockSessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationSession?)null);

        var service = new ConversationSummaryService(
            mockSummaryRepo.Object,
            mockSessionRepo.Object,
            mockMessageRepo.Object,
            mockGeminiClient.Object,
            mockLogger.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateSummaryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithNoMessages_CreatesEmptySummary()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockSummaryRepo = new Mock<IConversationSummaryRepository>();
        var mockSessionRepo = new Mock<IConversationSessionRepository>();
        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockGeminiClient = new Mock<IGeminiClient>();
        var mockLogger = new Mock<ILogger<ConversationSummaryService>>();

        var session = new ConversationSession
        {
            Id = sessionId,
            UserProfileId = userId,
            Channel = Channel.Website,
            Status = SessionStatus.Active,
            Language = Language.English,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        mockMessageRepo.Setup(r => r.GetRecentBySessionIdAsync(sessionId, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Message>());

        mockSummaryRepo.Setup(r => r.CreateAsync(It.IsAny<ConversationSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationSummary s, CancellationToken _) => s);

        mockSessionRepo.Setup(r => r.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ConversationSummaryService(
            mockSummaryRepo.Object,
            mockSessionRepo.Object,
            mockMessageRepo.Object,
            mockGeminiClient.Object,
            mockLogger.Object);

        var result = await service.GenerateSummaryAsync(sessionId);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Contains("topics", result.StructuredSummary);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithValidSession_ReturnsSummary()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockSummaryRepo = new Mock<IConversationSummaryRepository>();
        var mockSessionRepo = new Mock<IConversationSessionRepository>();
        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockGeminiClient = new Mock<IGeminiClient>();
        var mockLogger = new Mock<ILogger<ConversationSummaryService>>();
        GeminiRequest? capturedRequest = null;

        var session = new ConversationSession
        {
            Id = sessionId,
            UserProfileId = userId,
            Channel = Channel.Website,
            Status = SessionStatus.Active,
            Language = Language.English,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        var messages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Hello, I need a quotation",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = "Hello! I'd be happy to help you get a quotation.",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        mockMessageRepo.Setup(r => r.GetRecentBySessionIdAsync(sessionId, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        mockGeminiClient.Setup(c => c.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """{"topics":["quotation"],"decisions":[],"preferences":[],"entities":[],"intentCategories":["inquiry"],"unresolvedQuestions":[]}"""
            });

        mockSummaryRepo.Setup(r => r.CreateAsync(It.IsAny<ConversationSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationSummary s, CancellationToken _) => s);

        mockSessionRepo.Setup(r => r.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ConversationSummaryService(
            mockSummaryRepo.Object,
            mockSessionRepo.Object,
            mockMessageRepo.Object,
            mockGeminiClient.Object,
            mockLogger.Object);

        var result = await service.GenerateSummaryAsync(sessionId);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("gemini-2.5-flash-lite", capturedRequest!.ModelName);
        Assert.Equal("flex", capturedRequest!.ServiceTier);
        Assert.Equal("application/json", capturedRequest.ResponseMimeType);
        Assert.NotNull(capturedRequest.ResponseSchema);
        Assert.Equal(0, capturedRequest.ThinkingBudget);
        Assert.Equal(0.1, capturedRequest.Temperature);
        Assert.Equal(1024, capturedRequest.MaxTokens);
        Assert.Equal(30000, capturedRequest.MaxPromptTokens);
        mockSessionRepo.Verify(r => r.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithGeminiFailure_ReturnsEmptySummary()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockSummaryRepo = new Mock<IConversationSummaryRepository>();
        var mockSessionRepo = new Mock<IConversationSessionRepository>();
        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockGeminiClient = new Mock<IGeminiClient>();
        var mockLogger = new Mock<ILogger<ConversationSummaryService>>();

        var session = new ConversationSession
        {
            Id = sessionId,
            UserProfileId = userId,
            Channel = Channel.Website,
            Status = SessionStatus.Active,
            Language = Language.English,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        var messages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Hello",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        mockMessageRepo.Setup(r => r.GetRecentBySessionIdAsync(sessionId, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        mockGeminiClient.Setup(c => c.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse { Success = false, ErrorMessage = "API Error" });

        mockSummaryRepo.Setup(r => r.CreateAsync(It.IsAny<ConversationSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationSummary s, CancellationToken _) => s);

        mockSessionRepo.Setup(r => r.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ConversationSummaryService(
            mockSummaryRepo.Object,
            mockSessionRepo.Object,
            mockMessageRepo.Object,
            mockGeminiClient.Object,
            mockLogger.Object);

        var result = await service.GenerateSummaryAsync(sessionId);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result.SessionId);
    }

    [Fact]
    public async Task GenerateSummaryAsync_WithInvalidJson_ReturnsEmptySummary()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var mockSummaryRepo = new Mock<IConversationSummaryRepository>();
        var mockSessionRepo = new Mock<IConversationSessionRepository>();
        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockGeminiClient = new Mock<IGeminiClient>();
        var mockLogger = new Mock<ILogger<ConversationSummaryService>>();

        var session = new ConversationSession
        {
            Id = sessionId,
            UserProfileId = userId,
            Channel = Channel.Website,
            Status = SessionStatus.Active,
            Language = Language.English,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        var messages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Hello",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        mockSessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        mockMessageRepo.Setup(r => r.GetRecentBySessionIdAsync(sessionId, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        mockGeminiClient.Setup(c => c.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse { Success = true, Content = "This is not JSON" });

        mockSummaryRepo.Setup(r => r.CreateAsync(It.IsAny<ConversationSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationSummary s, CancellationToken _) => s);

        mockSessionRepo.Setup(r => r.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ConversationSummaryService(
            mockSummaryRepo.Object,
            mockSessionRepo.Object,
            mockMessageRepo.Object,
            mockGeminiClient.Object,
            mockLogger.Object);

        var result = await service.GenerateSummaryAsync(sessionId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetRecentSummariesAsync_ReturnsSummaries()
    {
        var userId = Guid.NewGuid();
        var summaries = new List<ConversationSummary>
        {
            new ConversationSummary { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), UserProfileId = userId },
            new ConversationSummary { Id = Guid.NewGuid(), SessionId = Guid.NewGuid(), UserProfileId = userId }
        };

        var mockSummaryRepo = new Mock<IConversationSummaryRepository>();
        var mockSessionRepo = new Mock<IConversationSessionRepository>();
        var mockMessageRepo = new Mock<IMessageRepository>();
        var mockGeminiClient = new Mock<IGeminiClient>();
        var mockLogger = new Mock<ILogger<ConversationSummaryService>>();

        mockSummaryRepo.Setup(r => r.GetRecentByUserProfileIdAsync(userId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summaries);

        var service = new ConversationSummaryService(
            mockSummaryRepo.Object,
            mockSessionRepo.Object,
            mockMessageRepo.Object,
            mockGeminiClient.Object,
            mockLogger.Object);

        var result = await service.GetRecentSummariesAsync(userId);

        Assert.Equal(2, result.Count());
    }
}

public class ResponseFormatterServiceTests
{
    [Fact]
    public void FormatResponse_WithThaiLanguage_ReturnsThaiButtons()
    {
        var service = new ResponseFormatterService();
        var (content, actions) = service.FormatResponse("test-content", Language.Thai);

        Assert.Equal("test-content", content);
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void FormatResponse_WithEnglishLanguage_ReturnsButtons()
    {
        var service = new ResponseFormatterService();
        var (content, actions) = service.FormatResponse("test-content", Language.English);

        Assert.Equal("test-content", content);
        Assert.NotNull(actions);
    }

    [Fact]
    public void GetWelcomeMessage_WithEnglish_ReturnsEnglishMessage()
    {
        var service = new ResponseFormatterService();
        var msg = service.GetWelcomeMessage(Language.English);

        Assert.Contains("Hello", msg);
    }

    [Fact]
    public void GetWelcomeMessage_WithThai_ReturnsThaiMessage()
    {
        var service = new ResponseFormatterService();
        var msg = service.GetWelcomeMessage(Language.Thai);

        Assert.NotNull(msg);
    }
}

public class LanguageDetectionServiceTests
{
    [Fact]
    public void DetectLanguage_WithThaiText_ReturnsThai()
    {
        var service = new LanguageDetectionService();
        var result = service.DetectLanguage("สวัสดีครับ ยินดีต้อนรับ");

        Assert.Equal(Language.Thai, result);
    }

    [Fact]
    public void DetectLanguage_WithEnglishText_ReturnsEnglish()
    {
        var service = new LanguageDetectionService();
        var result = service.DetectLanguage("Hello world");

        Assert.Equal(Language.English, result);
    }

    [Fact]
    public void DetectLanguage_WithEmptyString_ReturnsEnglish()
    {
        var service = new LanguageDetectionService();
        var result = service.DetectLanguage("");

        Assert.Equal(Language.English, result);
    }

    [Fact]
    public void DetectLanguage_WithWhitespace_ReturnsEnglish()
    {
        var service = new LanguageDetectionService();
        var result = service.DetectLanguage("   ");

        Assert.Equal(Language.English, result);
    }

    [Fact]
    public async Task DetectLanguageAsync_WithThaiText_ReturnsThai()
    {
        var service = new LanguageDetectionService();
        var result = await service.DetectLanguageAsync("สวัสดีครับ");

        Assert.Equal(Language.Thai, result);
    }

    [Fact]
    public async Task DetectLanguageAsync_WithEnglishText_ReturnsEnglish()
    {
        var service = new LanguageDetectionService();
        var result = await service.DetectLanguageAsync("Hello world");

        Assert.Equal(Language.English, result);
    }
}
