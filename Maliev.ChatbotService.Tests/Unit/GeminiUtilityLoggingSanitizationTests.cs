using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiUtilityLoggingSanitizationTests
{
    private const string SensitiveCustomerName = "Jane Customer";
    private const string SensitivePhone = "+66898950690";
    private const string SensitiveAddress = "36/1 Moo 3";

    [Fact]
    public async Task IntentClassificationService_ParseFailure_DoesNotLogRawPromptOrModelOutput()
    {
        var logger = new CapturingLogger<IntentClassificationService>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "null"
            });

        var instructionRepository = CreateInstructionRepository();
        var service = new IntentClassificationService(
            geminiClient.Object,
            instructionRepository.Object,
            new ConfigurationBuilder().Build(),
            logger);

        var result = await service.ClassifyIntentAsync(
            $"Find {SensitiveCustomerName} at {SensitiveAddress}, phone {SensitivePhone}");

        Assert.Equal("General", result.Intent);
        var logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain(SensitiveCustomerName, logs);
        Assert.DoesNotContain(SensitivePhone, logs);
        Assert.DoesNotContain(SensitiveAddress, logs);
        Assert.DoesNotContain("Raw content", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("result: null", logs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractCustomerIntentCommandHandler_ParseFailure_DoesNotLogRawModelOutput()
    {
        var logger = new CapturingLogger<ExtractCustomerIntentCommandHandler>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "null"
            });

        var handler = new ExtractCustomerIntentCommandHandler(
            geminiClient.Object,
            CreateInstructionRepository().Object,
            logger);

        var result = await handler.HandleAsync(new ExtractCustomerIntentCommand
        {
            UserMessage = $"Does {SensitiveCustomerName} have history?"
        });

        Assert.False(result.Success);
        var logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("result: null", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", logs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefineSystemInstructionCommandHandler_ParseFailure_DoesNotLogRawModelOutput()
    {
        var logger = new CapturingLogger<RefineSystemInstructionCommandHandler>();
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = $"Not JSON: {SensitiveCustomerName} {SensitivePhone} {SensitiveAddress}"
            });

        var handler = new RefineSystemInstructionCommandHandler(geminiClient.Object, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new RefineSystemInstructionCommand
            {
                Name = "Customer Website Assistant",
                Category = SystemInstructionCategory.Core,
                TopicKey = "website",
                PersonaDefinition = "Answer website questions.",
                BusinessConstraints = "Keep customer data safe."
            }));

        var logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain(SensitiveCustomerName, logs);
        Assert.DoesNotContain(SensitivePhone, logs);
        Assert.DoesNotContain(SensitiveAddress, logs);
    }

    private static Mock<ISystemInstructionRepository> CreateInstructionRepository()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());
        return instructionRepository;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
