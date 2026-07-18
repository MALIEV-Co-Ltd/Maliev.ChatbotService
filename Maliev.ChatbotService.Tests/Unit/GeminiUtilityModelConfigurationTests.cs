using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiUtilityModelConfigurationTests
{
    [Fact]
    public async Task CleanDictationSpeechCommandHandler_UsesConfiguredGeminiUtilityModel()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = CreateGeminiClientMock(request => capturedRequest = request, "Cleaned quote request.");
        var handler = CreateWithConfiguration<CleanDictationSpeechCommandHandler>(
            "gemini-2.5-flash-lite-configured",
            services =>
            {
                services.AddSingleton(geminiClient.Object);
                services.AddSingleton<ILogger<CleanDictationSpeechCommandHandler>>(
                    NullLogger<CleanDictationSpeechCommandHandler>.Instance);
            });

        await handler.HandleAsync(new CleanDictationSpeechCommand
        {
            Speech = "um please quote this part"
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("gemini-2.5-flash-lite-configured", capturedRequest!.ModelName);
    }

    [Fact]
    public async Task ExtractCustomerIntentCommandHandler_UsesConfiguredGeminiUtilityModel()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = CreateGeminiClientMock(
            request => capturedRequest = request,
            "{\"needs_customer_data\":true,\"customer_search_term\":\"Acme\",\"needs_history\":false}");
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());

        var handler = CreateWithConfiguration<ExtractCustomerIntentCommandHandler>(
            "gemini-2.5-flash-lite-configured",
            services =>
            {
                services.AddSingleton(geminiClient.Object);
                services.AddSingleton(instructionRepository.Object);
                services.AddSingleton<ILogger<ExtractCustomerIntentCommandHandler>>(
                    NullLogger<ExtractCustomerIntentCommandHandler>.Instance);
            });

        await handler.HandleAsync(new ExtractCustomerIntentCommand
        {
            UserMessage = "Find Acme contact info"
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("gemini-2.5-flash-lite-configured", capturedRequest!.ModelName);
    }

    [Fact]
    public async Task ExtractCustomerCommandHandler_UsesConfiguredGeminiUtilityModel()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = CreateGeminiClientMock(
            request => capturedRequest = request,
            """{"first_name":"Jane","last_name":"Customer"}""");
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();

        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());
        modelContextCacheService
            .Setup(item => item.GetOrCreateSystemInstructionCacheAsync(
                It.IsAny<ModelContextCacheRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelContextCacheReference?)null);

        var handler = CreateWithConfiguration<ExtractCustomerCommandHandler>(
            "gemini-2.5-flash-lite-configured",
            services =>
            {
                services.AddSingleton(geminiClient.Object);
                services.AddSingleton(instructionRepository.Object);
                services.AddSingleton(modelContextCacheService.Object);
                services.AddSingleton(modelFileStagingService.Object);
                services.AddSingleton<ILogger<ExtractCustomerCommandHandler>>(
                    NullLogger<ExtractCustomerCommandHandler>.Instance);
            });

        await handler.HandleAsync(new ExtractCustomerCommand
        {
            RawText = "Jane Customer, jane@example.com"
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("gemini-2.5-flash-lite-configured", capturedRequest!.ModelName);
    }

    [Fact]
    public async Task ConversationSummaryService_UsesConfiguredGeminiUtilityModel()
    {
        var sessionId = Guid.NewGuid();
        var userProfileId = Guid.NewGuid();
        GeminiRequest? capturedRequest = null;
        var geminiClient = CreateGeminiClientMock(
            request => capturedRequest = request,
            """{"topics":["quotation"],"decisions":[],"preferences":[],"entities":[],"intentCategories":["inquiry"],"unresolvedQuestions":[]}""");
        var summaryRepository = new Mock<IConversationSummaryRepository>();
        summaryRepository
            .Setup(item => item.CreateAsync(It.IsAny<ConversationSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationSummary summary, CancellationToken _) => summary);
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
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-30),
                LastActivityAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });
        sessionRepository
            .Setup(item => item.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var messageRepository = new Mock<IMessageRepository>();
        messageRepository
            .Setup(item => item.GetRecentBySessionIdAsync(sessionId, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Message
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    Role = MessageRole.User,
                    Content = "I need a quote.",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            ]);

        var service = CreateWithConfiguration<ConversationSummaryService>(
            "gemini-2.5-flash-lite-configured",
            services =>
            {
                services.AddSingleton(summaryRepository.Object);
                services.AddSingleton(sessionRepository.Object);
                services.AddSingleton(messageRepository.Object);
                services.AddSingleton(geminiClient.Object);
                services.AddSingleton<ILogger<ConversationSummaryService>>(
                    NullLogger<ConversationSummaryService>.Instance);
            });

        await service.GenerateSummaryAsync(sessionId);

        Assert.NotNull(capturedRequest);
        Assert.Equal("gemini-2.5-flash-lite-configured", capturedRequest!.ModelName);
    }

    [Fact]
    public async Task RefineSystemInstructionCommandHandler_UsesConfiguredGeminiUtilityModel()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = CreateGeminiClientMock(
            request => capturedRequest = request,
            """
            {
              "persona_definition": "Improved persona.",
              "business_constraints": "Improved constraints.",
              "summary": "Improved clarity."
            }
            """);
        var handler = CreateWithConfiguration<RefineSystemInstructionCommandHandler>(
            "gemini-2.5-flash-lite-configured",
            services =>
            {
                services.AddSingleton(geminiClient.Object);
                services.AddSingleton<ILogger<RefineSystemInstructionCommandHandler>>(
                    NullLogger<RefineSystemInstructionCommandHandler>.Instance);
            });

        await handler.HandleAsync(new RefineSystemInstructionCommand
        {
            Name = "Customer extraction",
            Category = SystemInstructionCategory.Topic,
            PersonaDefinition = "Extract customer data.",
            BusinessConstraints = "Return JSON."
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("gemini-2.5-flash-lite-configured", capturedRequest!.ModelName);
    }

    private static T CreateWithConfiguration<T>(
        string modelName,
        Action<IServiceCollection> configureServices)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:IntentModelName"] = modelName
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        configureServices(services);
        return ActivatorUtilities.CreateInstance<T>(services.BuildServiceProvider());
    }

    private static Mock<IGeminiClient> CreateGeminiClientMock(
        Action<GeminiRequest> capture,
        string content)
    {
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capture(request))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = content
            });
        return geminiClient;
    }
}
