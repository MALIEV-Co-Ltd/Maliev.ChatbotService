using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiUtilityOutputBoundTests
{
    [Fact]
    public async Task ExtractCustomerIntentCommandHandler_BoundsGeneratedOutputTokens()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "{\"needs_customer_data\":true,\"customer_search_term\":\"Acme\",\"needs_history\":false}"
            });

        var instructionRepository = new Mock<ISystemInstructionRepository>();
        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());

        var handler = new ExtractCustomerIntentCommandHandler(
            geminiClient.Object,
            instructionRepository.Object,
            NullLogger<ExtractCustomerIntentCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ExtractCustomerIntentCommand
        {
            UserMessage = "Find Acme contact info"
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(128, capturedRequest!.MaxTokens);
        Assert.Equal(4096, capturedRequest.MaxPromptTokens);
        Assert.Null(capturedRequest.ServiceTier);
        Assert.Equal(5, capturedRequest.TimeoutSeconds);
    }

    [Fact]
    public async Task ExtractCustomerIntentCommandHandler_WhenUtilityFlexConfigured_UsesFlexServiceTier()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "{\"needs_customer_data\":true,\"customer_search_term\":\"Acme\",\"needs_history\":false}"
            });

        var instructionRepository = new Mock<ISystemInstructionRepository>();
        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());

        var configuration = CreateUtilityFlexConfiguration();
        var handler = new ExtractCustomerIntentCommandHandler(
            geminiClient.Object,
            instructionRepository.Object,
            NullLogger<ExtractCustomerIntentCommandHandler>.Instance,
            configuration);

        var result = await handler.HandleAsync(new ExtractCustomerIntentCommand
        {
            UserMessage = "Find Acme contact info"
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("flex", capturedRequest!.ServiceTier);
        Assert.Equal(GeminiRequest.FlexInferenceTimeoutSeconds, capturedRequest.TimeoutSeconds);
    }

    [Fact]
    public async Task CleanDictationSpeechCommandHandler_BoundsGeneratedOutputTokens()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "Please quote this part."
            });

        var handler = new CleanDictationSpeechCommandHandler(
            geminiClient.Object,
            NullLogger<CleanDictationSpeechCommandHandler>.Instance);

        var result = await handler.HandleAsync(new CleanDictationSpeechCommand
        {
            Speech = "um please quote this part"
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(1024, capturedRequest!.MaxTokens);
        Assert.Equal(4096, capturedRequest.MaxPromptTokens);
        Assert.Null(capturedRequest.ServiceTier);
        Assert.Equal(5, capturedRequest.TimeoutSeconds);
    }

    [Fact]
    public async Task CleanDictationSpeechCommandHandler_WhenUtilityFlexConfigured_UsesFlexServiceTier()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "Please quote this part."
            });

        var configuration = CreateUtilityFlexConfiguration();
        var handler = new CleanDictationSpeechCommandHandler(
            geminiClient.Object,
            NullLogger<CleanDictationSpeechCommandHandler>.Instance,
            configuration);

        var result = await handler.HandleAsync(new CleanDictationSpeechCommand
        {
            Speech = "um please quote this part"
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("flex", capturedRequest!.ServiceTier);
        Assert.Equal(GeminiRequest.FlexInferenceTimeoutSeconds, capturedRequest.TimeoutSeconds);
    }

    [Fact]
    public async Task RefineSystemInstructionCommandHandler_BoundsGeneratedOutputTokens()
    {
        GeminiRequest? capturedRequest = null;
        var geminiClient = new Mock<IGeminiClient>();
        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "{\"persona_definition\":\"Refined persona\",\"business_constraints\":\"Refined constraints\",\"summary\":\"Improved clarity.\"}"
            });

        var handler = new RefineSystemInstructionCommandHandler(
            geminiClient.Object,
            NullLogger<RefineSystemInstructionCommandHandler>.Instance);

        var result = await handler.HandleAsync(new RefineSystemInstructionCommand
        {
            Name = "Customer Website Assistant",
            Category = SystemInstructionCategory.Core,
            TopicKey = "website",
            PersonaDefinition = "Answer website questions.",
            BusinessConstraints = "Keep customer data safe."
        });

        Assert.Equal("Refined persona", result.PersonaDefinition);
        Assert.NotNull(capturedRequest);
        Assert.Equal(4096, capturedRequest!.MaxTokens);
        Assert.Equal(16000, capturedRequest.MaxPromptTokens);
        Assert.Equal("flex", capturedRequest.ServiceTier);
        Assert.Equal(600, capturedRequest.TimeoutSeconds);
    }

    private static IConfiguration CreateUtilityFlexConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:UtilityRequests:ServiceTier"] = "flex"
            })
            .Build();
    }
}
