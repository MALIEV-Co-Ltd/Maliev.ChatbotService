using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ExtractCustomerCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithFileAttachments_UsesMediumMediaResolution()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        GeminiRequest? capturedRequest = null;

        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>());
        modelContextCacheService
            .Setup(item => item.GetOrCreateSystemInstructionCacheAsync(
                It.IsAny<ModelContextCacheRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelContextCacheReference?)null);

        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """{"first_name":"Jane","last_name":"Customer","addresses":[{"address_line_1":"1 Test Road"}]}"""
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            Files =
            [
                new ExtractionFileData
                {
                    FileName = "customer-form.pdf",
                    MimeType = "application/pdf",
                    Base64Data = Convert.ToBase64String("test document"u8)
                }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("MEDIA_RESOLUTION_MEDIUM", capturedRequest!.MediaResolution);
        Assert.Equal(20000, capturedRequest.MaxPromptTokens);
        Assert.Equal(4096, capturedRequest.MaxTokens);
        Assert.Equal("flex", capturedRequest.ServiceTier);
        Assert.Equal(600, capturedRequest.TimeoutSeconds);
        Assert.NotNull(capturedRequest.Attachments);
        Assert.Single(capturedRequest.Attachments);
        Assert.Equal("application/pdf", capturedRequest.Attachments![0].MimeType);
    }

    [Fact]
    public async Task HandleAsync_SystemPromptCacheHit_UsesCachedContentWithoutDuplicatingPrompt()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        GeminiRequest? capturedRequest = null;

        instructionRepository
            .Setup(item => item.GetActiveByTopicsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SystemInstruction>
            {
                new()
                {
                    PersonaDefinition = "Extract customer information.",
                    BusinessConstraints = "Return only verified fields."
                }
            });
        modelContextCacheService
            .Setup(item => item.GetOrCreateSystemInstructionCacheAsync(
                It.IsAny<ModelContextCacheRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelContextCacheReference { CachedContentName = "cachedContents/customer-extraction" });

        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """{"first_name":"Jane","last_name":"Customer"}"""
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            RawText = "Jane Customer, jane@example.com"
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("cachedContents/customer-extraction", capturedRequest!.CachedContentName);
        Assert.Equal(string.Empty, capturedRequest.SystemInstruction);
        Assert.Equal("application/json", capturedRequest.ResponseMimeType);
        Assert.Equal("flex", capturedRequest.ServiceTier);
        modelContextCacheService.Verify(item => item.GetOrCreateSystemInstructionCacheAsync(
            It.Is<ModelContextCacheRequest>(request =>
                request.SystemInstruction == "Extract customer information.\n\nReturn only verified fields." &&
                request.ModelName == "gemini-2.5-flash-lite"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
