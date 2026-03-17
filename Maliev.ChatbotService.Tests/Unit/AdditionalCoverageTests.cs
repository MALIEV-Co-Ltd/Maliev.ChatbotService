using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Maliev.ChatbotService.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class AdditionalCoverageExtractPrefsTests
{
    [Fact]
    public async Task ExtractPrefs_WithNullMessage_ReturnsEmptyList()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);
        
        var result = await service.ExtractPreferencesAsync(null!);
        
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExtractPrefs_WithNoMatchingPatterns_ReturnsEmptyOrSmallList()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);
        
        var result = await service.ExtractPreferencesAsync("Hello there, how are you?");
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExtractPrefs_WithThaiMaterialPreference_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);
        
        var result = await service.ExtractPreferencesAsync("I prefer อลูมิเนียม material");
        
        Assert.NotEmpty(result);
        var materialPref = result.FirstOrDefault(r => r.Key == "MaterialPreference");
        Assert.NotNull(materialPref);
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, string>>(materialPref.Value);
        Assert.NotNull(deserialized);
        Assert.Contains("อลูมิเนียม", deserialized.GetValueOrDefault("material") ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractPrefs_WithProcessCNC_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);
        
        var result = await service.ExtractPreferencesAsync("My preference is 3D printing process");
        
        Assert.NotEmpty(result);
        var processPref = result.FirstOrDefault(r => r.Key == "ProcessPreference");
        Assert.NotNull(processPref);
    }

    [Fact]
    public async Task ExtractPrefs_WithQualityDIN_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);
        
        var result = await service.ExtractPreferencesAsync("We require DIN standard");
        
        Assert.NotEmpty(result);
        var qualityPref = result.FirstOrDefault(r => r.Key == "QualityStandard");
        Assert.NotNull(qualityPref);
    }

    [Fact]
    public async Task ExtractPrefs_WithDeliveryWeeks_ExtractsPreference()
    {
        var service = new ExtractPreferencesService(NullLogger<ExtractPreferencesService>.Instance);
        
        var result = await service.ExtractPreferencesAsync("I need it delivered in 2 weeks");
        
        Assert.NotEmpty(result);
        var deliveryPref = result.FirstOrDefault(r => r.Key == "DeliveryPreference");
        Assert.NotNull(deliveryPref);
    }
}

public class AdditionalCoverageSummaryTests
{
    [Fact]
    public async Task Summary_WithMarkdownJsonResponse_CleansResponse()
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
            .ReturnsAsync(new GeminiResponse 
            { 
                Success = true, 
                Content = "```json\n{\"topics\":[\"test\"],\"decisions\":[],\"preferences\":[],\"entities\":[],\"intentCategories\":[],\"unresolvedQuestions\":[]}\n```" 
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
        Assert.Contains("topics", result.StructuredSummary);
    }

    [Fact]
    public async Task Summary_WithPlainCodeBlock_CleansResponse()
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
            .ReturnsAsync(new GeminiResponse 
            { 
                Success = true, 
                Content = "```\n{\"topics\":[\"test\"],\"decisions\":[],\"preferences\":[],\"entities\":[],\"intentCategories\":[],\"unresolvedQuestions\":[]}\n```" 
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
    }
}

public class AdditionalCoverageToolRegistryTests
{
    [Fact]
    public void ToolRegistry_GetDeclarations_ReturnsNonEmpty()
    {
        var declarations = ToolRegistry.GetAllToolDeclarations();
        
        Assert.NotNull(declarations);
        Assert.NotEmpty(declarations);
    }

    [Fact]
    public void ToolRegistry_DeclarationsHaveValidFunctions()
    {
        var declarations = ToolRegistry.GetAllToolDeclarations();
        
        foreach (var decl in declarations)
        {
            Assert.NotNull(decl.FunctionDeclarations);
            foreach (var fn in decl.FunctionDeclarations!)
            {
                Assert.False(string.IsNullOrEmpty(fn.Name));
                Assert.False(string.IsNullOrEmpty(fn.Description));
                Assert.NotNull(fn.Parameters);
            }
        }
    }
}
