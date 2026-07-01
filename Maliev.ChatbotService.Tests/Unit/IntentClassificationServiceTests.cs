using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class IntentClassificationServiceTests
{
    [Fact]
    public async Task ClassifyIntentAsync_BuildsBoundedStructuredGeminiRequest()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "{\"intent\":\"Sales\",\"confidence\":0.91,\"additionalTopics\":[\"Quotation\"]}"
            });

        var instructionRepository = new Mock<ISystemInstructionRepository>();
        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());

        var configuration = new ConfigurationBuilder().Build();
        var service = new IntentClassificationService(
            geminiClient.Object,
            instructionRepository.Object,
            configuration,
            NullLogger<IntentClassificationService>.Instance);

        var result = await service.ClassifyIntentAsync("I need a quote for 100 CNC parts");

        Assert.Equal("Sales", result.Intent);
        Assert.Equal("gemini-2.5-flash-lite", capturedRequest!.ModelName);
        Assert.Equal("application/json", capturedRequest.ResponseMimeType);
        Assert.NotNull(capturedRequest.ResponseSchema);
        Assert.Equal(256, capturedRequest.MaxTokens);
        Assert.Equal(4096, capturedRequest.MaxPromptTokens);
        Assert.Equal(0, capturedRequest.ThinkingBudget);
        Assert.Equal(0.1, capturedRequest.Temperature);
        Assert.Equal(5, capturedRequest.TimeoutSeconds);

        var schemaJson = JsonSerializer.Serialize(capturedRequest.ResponseSchema);
        Assert.Contains("intent", schemaJson);
        Assert.Contains("confidence", schemaJson);
        Assert.Contains("additionalTopics", schemaJson);
    }
}
