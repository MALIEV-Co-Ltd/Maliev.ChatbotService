using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class SystemInstructionDefaultPromptTests
{
    [Fact]
    public async Task GetMergedInstructionsAsync_QuoteEngineDefaultPrompt_InstructsTrustedAuthHandoffForCheckout()
    {
        var repository = new Mock<ISystemInstructionRepository>();
        repository
            .Setup(item => item.GetActiveCoreAsync("quote-engine", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);
        repository
            .Setup(item => item.GetActiveCoreAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);

        var cache = new Mock<ICacheService>();
        cache
            .Setup(item => item.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        cache
            .Setup(item => item.GetAsync<SystemInstruction>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);

        var metrics = new Mock<IConversationMetrics>();
        var service = new SystemInstructionService(
            repository.Object,
            cache.Object,
            metrics.Object,
            NullLogger<SystemInstructionService>.Instance);

        var prompt = await service.GetMergedInstructionsAsync([], "quote-engine");

        Assert.Contains("quote_get_auth_handoff", prompt, StringComparison.Ordinal);
        Assert.Contains("sign-in or sign-up", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checkout", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never collect credentials in chat", prompt, StringComparison.Ordinal);
    }
}
