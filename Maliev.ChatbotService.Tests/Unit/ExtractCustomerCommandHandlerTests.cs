using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ExtractCustomerCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithStoragePathsOnly_DoesNotCallGemini()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();

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
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """{"first_name":"Jane","last_name":"Customer"}"""
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            modelFileStagingService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            StoragePaths = ["uploads/customer-form.pdf"]
        });

        Assert.False(result.Success);
        Assert.Contains("storage paths", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        geminiClient.Verify(
            item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithFileAttachments_UsesMediumMediaResolution()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelFileReference?)null);

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
            modelFileStagingService.Object,
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
        Assert.False(capturedRequest.Store.GetValueOrDefault(true));
        Assert.NotNull(capturedRequest.Attachments);
        Assert.Single(capturedRequest.Attachments);
        Assert.Equal("application/pdf", capturedRequest.Attachments![0].MimeType);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulExtraction_DoesNotLogRawCustomerData()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        var logger = new CapturingLogger<ExtractCustomerCommandHandler>();

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
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """
                    {
                      "first_name": "Jane",
                      "last_name": "Customer",
                      "mobile": "+66898950690",
                      "addresses": [
                        {
                          "address_line_1": "36/1 Moo 3",
                          "district": "Khlong Suan Phlu",
                          "city": "Ayutthaya",
                          "postal_code": "13000"
                        }
                      ]
                    }
                    """
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            modelFileStagingService.Object,
            logger);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            RawText = "Jane Customer, +66898950690, 36/1 Moo 3, Ayutthaya 13000"
        });

        Assert.True(result.Success);
        var logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("Jane", logs);
        Assert.DoesNotContain("+66898950690", logs);
        Assert.DoesNotContain("36/1 Moo 3", logs);
        Assert.DoesNotContain("13000", logs);
    }

    [Fact]
    public async Task HandleAsync_InvalidExtractionJson_DoesNotLogRawResponseContent()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        var logger = new CapturingLogger<ExtractCustomerCommandHandler>();

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
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "Jane Customer, +66898950690, 36/1 Moo 3, Ayutthaya 13000"
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            modelFileStagingService.Object,
            logger);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            RawText = "Jane Customer, +66898950690, 36/1 Moo 3, Ayutthaya 13000"
        });

        Assert.False(result.Success);
        var logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("Jane Customer", logs);
        Assert.DoesNotContain("+66898950690", logs);
        Assert.DoesNotContain("36/1 Moo 3", logs);
        Assert.DoesNotContain("13000", logs);
    }

    [Fact]
    public async Task HandleAsync_WithConfiguredMediaResolution_UsesConfiguredGeminiMediaResolution()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        GeminiRequest? capturedRequest = null;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:Extraction:MediaResolution"] = "low"
            })
            .Build();

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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelFileReference?)null);

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
            modelFileStagingService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance,
            configuration);

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
        Assert.Equal("MEDIA_RESOLUTION_LOW", capturedRequest!.MediaResolution);
    }

    [Fact]
    public void Constructor_WithUnsupportedMediaResolution_ThrowsConfigurationError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:Extraction:MediaResolution"] = "max-detail"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => new ExtractCustomerCommandHandler(
            Mock.Of<ISystemInstructionRepository>(),
            Mock.Of<IGeminiClient>(),
            Mock.Of<IModelContextCacheService>(),
            Mock.Of<IModelFileStagingService>(),
            NullLogger<ExtractCustomerCommandHandler>.Instance,
            configuration));

        Assert.Contains("Gemini:Extraction:MediaResolution", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_WithOversizedFileAttachment_StagesFileBeforeGeminiRequest()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        GeminiRequest? capturedRequest = null;
        ModelFileStagingRequest? capturedStagingRequest = null;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:FileApiInlineThresholdBytes"] = "8"
            })
            .Build();

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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ModelFileStagingRequest, CancellationToken>((request, _) => capturedStagingRequest = request)
            .ReturnsAsync(new ModelFileReference
            {
                FileUri = "https://generativelanguage.googleapis.com/v1beta/files/customer-form",
                MimeType = "application/pdf"
            });

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
            modelFileStagingService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance,
            configuration);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            Files =
            [
                new ExtractionFileData
                {
                    FileName = "customer-form.pdf",
                    MimeType = "application/pdf",
                    Base64Data = Convert.ToBase64String("large document payload"u8)
                }
            ]
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedStagingRequest);
        Assert.Equal("customer-form.pdf", capturedStagingRequest!.FileName);
        Assert.Equal("application/pdf", capturedStagingRequest.MimeType);
        Assert.Equal("large document payload"u8.ToArray(), capturedStagingRequest.Content);
        Assert.NotNull(capturedRequest);
        var attachment = Assert.Single(capturedRequest!.Attachments!);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/files/customer-form", attachment.Data);
        Assert.Equal("application/pdf", attachment.MimeType);
    }

    [Fact]
    public async Task HandleAsync_WithStagedFile_DeletesFileAfterSuccessfulGeminiCall()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:FileApiInlineThresholdBytes"] = "8"
            })
            .Build();

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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelFileReference
            {
                Name = "files/customer-form",
                FileUri = "https://generativelanguage.googleapis.com/v1beta/files/customer-form",
                MimeType = "application/pdf"
            });
        modelFileStagingService
            .Setup(item => item.DeleteFileAsync("files/customer-form", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """{"first_name":"Jane","last_name":"Customer"}"""
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            modelFileStagingService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance,
            configuration);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            Files =
            [
                new ExtractionFileData
                {
                    FileName = "customer-form.pdf",
                    MimeType = "application/pdf",
                    Base64Data = Convert.ToBase64String("large document payload"u8)
                }
            ]
        });

        Assert.True(result.Success);
        modelFileStagingService.Verify(
            item => item.DeleteFileAsync("files/customer-form", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithStagedFile_DeletesFileAfterFailedGeminiCall()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:FileApiInlineThresholdBytes"] = "8"
            })
            .Build();

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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelFileReference
            {
                Name = "files/customer-form",
                FileUri = "https://generativelanguage.googleapis.com/v1beta/files/customer-form",
                MimeType = "application/pdf"
            });
        modelFileStagingService
            .Setup(item => item.DeleteFileAsync("files/customer-form", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = false,
                ErrorMessage = "Gemini unavailable",
                ErrorType = "server_error"
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            modelFileStagingService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance,
            configuration);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            Files =
            [
                new ExtractionFileData
                {
                    FileName = "customer-form.pdf",
                    MimeType = "application/pdf",
                    Base64Data = Convert.ToBase64String("large document payload"u8)
                }
            ]
        });

        Assert.False(result.Success);
        Assert.Equal("Gemini unavailable", result.ErrorMessage);
        modelFileStagingService.Verify(
            item => item.DeleteFileAsync("files/customer-form", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithStagedFileDeleteFailure_ReturnsSuccessfulExtraction()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:FileApiInlineThresholdBytes"] = "8"
            })
            .Build();

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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelFileReference
            {
                Name = "files/customer-form",
                FileUri = "https://generativelanguage.googleapis.com/v1beta/files/customer-form",
                MimeType = "application/pdf"
            });
        modelFileStagingService
            .Setup(item => item.DeleteFileAsync("files/customer-form", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("delete failed"));

        geminiClient
            .Setup(item => item.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = """{"first_name":"Jane","last_name":"Customer"}"""
            });

        var handler = new ExtractCustomerCommandHandler(
            instructionRepository.Object,
            geminiClient.Object,
            modelContextCacheService.Object,
            modelFileStagingService.Object,
            NullLogger<ExtractCustomerCommandHandler>.Instance,
            configuration);

        var result = await handler.HandleAsync(new ExtractCustomerCommand
        {
            Files =
            [
                new ExtractionFileData
                {
                    FileName = "customer-form.pdf",
                    MimeType = "application/pdf",
                    Base64Data = Convert.ToBase64String("large document payload"u8)
                }
            ]
        });

        Assert.True(result.Success);
        modelFileStagingService.Verify(
            item => item.DeleteFileAsync("files/customer-form", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SystemPromptCacheHit_UsesCachedContentWithoutDuplicatingPrompt()
    {
        var instructionRepository = new Mock<ISystemInstructionRepository>();
        var geminiClient = new Mock<IGeminiClient>();
        var modelContextCacheService = new Mock<IModelContextCacheService>();
        var modelFileStagingService = new Mock<IModelFileStagingService>();
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
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelFileReference?)null);

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
            modelFileStagingService.Object,
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
