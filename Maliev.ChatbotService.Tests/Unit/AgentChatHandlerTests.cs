using System.Text.Json;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Unit tests for AgentChatHandler.
/// </summary>
public class AgentChatHandlerTests
{
    private readonly Mock<IGeminiClient> _geminiClientMock;
    private readonly Mock<IToolExecutorService> _toolExecutorMock;
    private readonly Mock<ILogger<AgentChatHandler>> _loggerMock;
    private readonly AgentChatHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentChatHandlerTests"/> class.
    /// </summary>
    public AgentChatHandlerTests()
    {
        _geminiClientMock = new Mock<IGeminiClient>();
        _toolExecutorMock = new Mock<IToolExecutorService>();
        _loggerMock = new Mock<ILogger<AgentChatHandler>>();
        _handler = new AgentChatHandler(_geminiClientMock.Object, _toolExecutorMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that when a tool returns document metadata, it is attached to the next request to Gemini.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithDocumentToolResult_AttachesFileToNextRequest()
    {
        // Arrange
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Read this NDA" } },
            Store = false
        };

        var pdfData = "JVBERi0xLjQKJ..."; // Mock base64
        var toolResult = JsonSerializer.Serialize(new
        {
            _metadata = new
            {
                is_file = true,
                mime_type = "application/pdf",
                data = pdfData
            },
            status = "Success",
            message = "Document content has been loaded"
        });

        // Use a list to capture calls for manual assertion
        var capturedRequests = new List<GeminiRequest>();
        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((r, c) => capturedRequests.Add(new GeminiRequest { Messages = new List<GeminiMessage>(r.Messages), Store = r.Store }))
            .ReturnsAsync((GeminiRequest r, CancellationToken c) =>
            {
                if (capturedRequests.Count == 1)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall { Name = "get_document_content", Args = new Dictionary<string, object> { ["document_id"] = "doc123" } }
                        }
                    };
                }
                return new GeminiResponse
                {
                    Success = true,
                    Content = "I have read the document. It is a standard NDA."
                };
            });

        _toolExecutorMock.Setup(x => x.ExecuteAsync("get_document_content", It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _handler.ExecuteAsync(initialRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, capturedRequests.Count);
        Assert.All(capturedRequests, request => Assert.False(request.Store.GetValueOrDefault(true)));

        // First call: 1 message, no attachments
        Assert.Single(capturedRequests[0].Messages);
        Assert.Null(capturedRequests[0].Messages[0].Attachments);

        // Second call: 3 messages, last one has attachment
        Assert.Equal(3, capturedRequests[1].Messages.Count);
        var lastMessage = capturedRequests[1].Messages[2];
        Assert.NotNull(lastMessage.Attachments);
        Assert.Single(lastMessage.Attachments!);
        Assert.Equal("application/pdf", lastMessage.Attachments![0].MimeType);
        Assert.Equal(pdfData, lastMessage.Attachments![0].Data);
    }

    /// <summary>
    /// Verifies that large tool-returned file payloads are staged before the next model call.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ToolFileAboveInlineThreshold_StagesFileForNextRequestAndCleansUp()
    {
        var modelFileStagingService = new Mock<IModelFileStagingService>();
        ModelFileStagingRequest? capturedStagingRequest = null;
        modelFileStagingService
            .Setup(item => item.StageFileAsync(It.IsAny<ModelFileStagingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ModelFileStagingRequest, CancellationToken>((request, _) => capturedStagingRequest = request)
            .ReturnsAsync(new ModelFileReference
            {
                Name = "files/tool-document",
                FileUri = "https://generativelanguage.googleapis.com/v1beta/files/tool-document",
                MimeType = "application/pdf"
            });
        modelFileStagingService
            .Setup(item => item.DeleteFileAsync("files/tool-document", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:FileApiInlineThresholdBytes"] = "8"
            })
            .Build();
        var handler = new AgentChatHandler(
            _geminiClientMock.Object,
            _toolExecutorMock.Object,
            _loggerMock.Object,
            modelFileStagingService.Object,
            configuration);

        var fileBytes = Enumerable.Range(0, 16).Select(item => (byte)item).ToArray();
        var fileBase64 = Convert.ToBase64String(fileBytes);
        var toolResult = JsonSerializer.Serialize(new
        {
            _metadata = new
            {
                is_file = true,
                mime_type = "application/pdf",
                data = fileBase64
            },
            status = "Success",
            message = "Document content has been loaded"
        });

        var capturedRequests = new List<GeminiRequest>();
        _geminiClientMock
            .Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) =>
            {
                capturedRequests.Add(new GeminiRequest
                {
                    Messages = request.Messages.Select(message => new GeminiMessage
                    {
                        Role = message.Role,
                        Content = message.Content,
                        FunctionCalls = message.FunctionCalls,
                        FunctionResponses = message.FunctionResponses,
                        Attachments = message.Attachments?.Select(attachment => new GeminiAttachment
                        {
                            ContentType = attachment.ContentType,
                            MimeType = attachment.MimeType,
                            Data = attachment.Data
                        }).ToList()
                    }).ToList()
                });
            })
            .ReturnsAsync((GeminiRequest _, CancellationToken _) =>
            {
                if (capturedRequests.Count == 1)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new()
                            {
                                Name = "get_document_content",
                                Args = new Dictionary<string, object> { ["document_id"] = "doc123" }
                            }
                        }
                    };
                }

                return new GeminiResponse
                {
                    Success = true,
                    Content = "I have read the document."
                };
            });
        _toolExecutorMock
            .Setup(x => x.ExecuteAsync(
                "get_document_content",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        var result = await handler.ExecuteAsync(new GeminiRequest
        {
            Messages = new List<GeminiMessage>
            {
                new() { Role = "user", Content = "Read this document" }
            }
        });

        Assert.True(result.Success);
        Assert.NotNull(capturedStagingRequest);
        Assert.Equal("tool-result-get_document_content.pdf", capturedStagingRequest!.FileName);
        Assert.Equal("application/pdf", capturedStagingRequest.MimeType);
        Assert.Equal(fileBytes, capturedStagingRequest.Content);
        Assert.Equal(2, capturedRequests.Count);
        var toolResultAttachment = Assert.Single(capturedRequests[1].Messages[2].Attachments!);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/files/tool-document", toolResultAttachment.Data);
        Assert.Equal("application/pdf", toolResultAttachment.MimeType);
        modelFileStagingService.Verify(
            item => item.DeleteFileAsync("files/tool-document", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that standard tool results do not add attachments.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_StandardToolResult_NoAttachment()
    {
        // Arrange
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Who is customer 1?" } }
        };

        var toolResult = """{"name": "John Doe", "id": "1"}""";

        var capturedRequests = new List<GeminiRequest>();
        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((r, c) => capturedRequests.Add(new GeminiRequest { Messages = new List<GeminiMessage>(r.Messages) }))
            .ReturnsAsync((GeminiRequest r, CancellationToken c) =>
            {
                if (capturedRequests.Count == 1)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall { Name = "get_customer", Args = new Dictionary<string, object> { ["customer_id"] = "1" } }
                        }
                    };
                }
                return new GeminiResponse
                {
                    Success = true,
                    Content = "Customer 1 is John Doe."
                };
            });

        _toolExecutorMock.Setup(x => x.ExecuteAsync("get_customer", It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _handler.ExecuteAsync(initialRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, capturedRequests.Count);

        // Verify that no attachments were added in either call
        Assert.All(capturedRequests, r => Assert.All(r.Messages, m => Assert.Null(m.Attachments)));
    }

    /// <summary>
    /// Verifies that QuoteEngine tool calls receive the trusted signed agent context.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithQuoteAgentContext_ForwardsContextToToolExecutor()
    {
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Price this uploaded part" } }
        };

        ToolExecutionContext? capturedContext = null;
        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeminiRequest _, CancellationToken _) =>
            {
                if (capturedContext is null)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall
                            {
                                Name = "quote_get_state",
                                Args = new Dictionary<string, object>()
                            }
                        }
                    };
                }

                return new GeminiResponse
                {
                    Success = true,
                    Content = "I checked the quote state."
                };
            });

        _toolExecutorMock.Setup(x => x.ExecuteAsync(
                "quote_get_state",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, object>, ToolExecutionContext, CancellationToken>((_, _, context, _) => capturedContext = context)
            .ReturnsAsync("""{"summary":"ready"}""");

        var result = await _handler.ExecuteAsync(
            initialRequest,
            userToken: "customer-token",
            quoteAgentContextToken: "signed-quote-context");

        Assert.True(result.Success);
        Assert.NotNull(capturedContext);
        Assert.Equal("customer-token", capturedContext.UserToken);
        Assert.Equal("signed-quote-context", capturedContext.QuoteAgentContextToken);
        _toolExecutorMock.Verify(x => x.ExecuteAsync(
            "quote_get_state",
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that output token caps from the caller are preserved through each agent loop request.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MaxTokens_PreservesOutputCapForProviderCalls()
    {
        var initialRequest = new GeminiRequest
        {
            MaxTokens = 2048,
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Quote this part" } }
        };
        GeminiRequest? capturedRequest = null;

        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "I can help quote that part."
            });

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(2048, capturedRequest!.MaxTokens);
    }

    /// <summary>
    /// Verifies that prompt token caps from the caller are preserved through each agent loop request.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MaxPromptTokens_PreservesPromptTokenCapForProviderCalls()
    {
        var initialRequest = new GeminiRequest
        {
            MaxPromptTokens = 30000,
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Quote this uploaded part" } }
        };
        GeminiRequest? capturedRequest = null;

        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                Content = "I can help quote that part."
            });

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.True(result.Success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(30000, capturedRequest!.MaxPromptTokens);
    }

    /// <summary>
    /// Verifies that cached prompt prefixes are preserved through every provider request in the agent loop.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CachedContentName_PreservesProviderCacheAcrossIterations()
    {
        var initialRequest = new GeminiRequest
        {
            CachedContentName = "cachedContents/agent-system-prompt",
            SystemInstruction = string.Empty,
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Quote this part" } }
        };
        var capturedRequests = new List<GeminiRequest>();

        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequests.Add(request))
            .ReturnsAsync(() =>
            {
                if (capturedRequests.Count == 1)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall { Name = "quote_get_state", Args = new Dictionary<string, object>() }
                        }
                    };
                }

                return new GeminiResponse
                {
                    Success = true,
                    Content = "The quote state is ready."
                };
            });

        _toolExecutorMock.Setup(x => x.ExecuteAsync(
                "quote_get_state",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.True(result.Success);
        Assert.Equal(2, capturedRequests.Count);
        Assert.All(capturedRequests, request =>
        {
            Assert.Equal("cachedContents/agent-system-prompt", request.CachedContentName);
            Assert.Equal(string.Empty, request.SystemInstruction);
        });
    }

    /// <summary>
    /// Verifies that built-in Gemini web search stays enabled across every provider request in the agent loop.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_EnableWebSearch_PreservesBuiltInSearchAcrossIterations()
    {
        var initialRequest = new GeminiRequest
        {
            EnableWebSearch = true,
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Find the latest ISO material guidance" } }
        };
        var capturedRequests = new List<GeminiRequest>();

        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((request, _) => capturedRequests.Add(request))
            .ReturnsAsync(() =>
            {
                if (capturedRequests.Count == 1)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall { Name = "quote_get_state", Args = new Dictionary<string, object>() }
                        }
                    };
                }

                return new GeminiResponse
                {
                    Success = true,
                    Content = "Here is the current guidance."
                };
            });

        _toolExecutorMock.Setup(x => x.ExecuteAsync(
                "quote_get_state",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.True(result.Success);
        Assert.Equal(2, capturedRequests.Count);
        Assert.All(capturedRequests, request => Assert.True(request.EnableWebSearch));
    }

    /// <summary>
    /// Verifies that streamed final assistant text is emitted as deltas from the agent loop.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithTextDeltaCallback_StreamsAssistantDeltas()
    {
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Quote this part" } }
        };
        var deltas = new List<string>();

        _geminiClientMock.Setup(x => x.StreamMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Returns(CreateStream([
                new GeminiStreamEvent { Type = "started" },
                new GeminiStreamEvent { Type = "delta", Delta = "Upload " },
                new GeminiStreamEvent { Type = "delta", Delta = "a CAD file." },
                new GeminiStreamEvent
                {
                    Type = "final",
                    Response = new GeminiResponse
                    {
                        Success = true,
                        Content = "Upload a CAD file."
                    }
                }
            ]));

        var result = await _handler.ExecuteAsync(
            initialRequest,
            onTextDelta: delta =>
            {
                deltas.Add(delta);
                return Task.CompletedTask;
            });

        Assert.True(result.Success);
        Assert.Equal("Upload a CAD file.", result.Content);
        Assert.Equal(["Upload ", "a CAD file."], deltas);
        _geminiClientMock.Verify(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that streamed model reasoning is forwarded as thought deltas from the agent loop, and
    /// that the per-iteration request preserves IncludeThoughts so the provider actually emits thoughts.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithThoughtDeltaCallback_StreamsModelReasoning()
    {
        var initialRequest = new GeminiRequest
        {
            IncludeThoughts = true,
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Quote this part" } }
        };
        var thoughts = new List<string>();
        GeminiRequest? capturedRequest = null;

        _geminiClientMock.Setup(x => x.StreamMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(CreateStream([
                new GeminiStreamEvent { Type = "started" },
                new GeminiStreamEvent { Type = "thought", Thought = "The customer wants " },
                new GeminiStreamEvent { Type = "thought", Thought = "a price for FDM." },
                new GeminiStreamEvent { Type = "delta", Delta = "Sure." },
                new GeminiStreamEvent
                {
                    Type = "final",
                    Response = new GeminiResponse
                    {
                        Success = true,
                        Content = "Sure."
                    }
                }
            ]));

        var result = await _handler.ExecuteAsync(
            initialRequest,
            onTextDelta: _ => Task.CompletedTask,
            onThoughtDelta: thought =>
            {
                thoughts.Add(thought);
                return Task.CompletedTask;
            });

        Assert.True(result.Success);
        Assert.Equal(["The customer wants ", "a price for FDM."], thoughts);
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.IncludeThoughts);
    }

    /// <summary>
    /// Verifies that provider fallback responses remain fallback results after the agent loop.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_GeminiFallback_PreservesFallbackFlag()
    {
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage>
            {
                new GeminiMessage { Role = "user", Content = "Quote this part" }
            }
        };

        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = false,
                IsFallback = true,
                Content = "The assistant is temporarily unavailable.",
                ErrorMessage = "The assistant is temporarily unavailable."
            });

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.False(result.Success);
        Assert.True(result.IsFallback);
        Assert.Equal("The assistant is temporarily unavailable.", result.Content);
    }

    /// <summary>
    /// Verifies that a model repeatedly requesting the same tool stops being executed after the
    /// per-turn call limit, bounding downstream cost (C5).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RepeatedSameTool_StopsExecutingAfterPerTurnLimit()
    {
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "loop" } }
        };

        // The model always asks for the same tool and never returns final text, so the loop runs to
        // its max iterations (10). The executor must still only be hit up to the per-tool limit.
        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse
            {
                Success = true,
                FunctionCalls = new List<GeminiFunctionCall>
                {
                    new GeminiFunctionCall { Name = "quote_get_state", Args = new Dictionary<string, object>() }
                }
            });

        var executionCount = 0;
        _toolExecutorMock.Setup(x => x.ExecuteAsync(
                "quote_get_state",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => executionCount++)
            .ReturnsAsync("{}");

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.Equal(3, executionCount);
        _toolExecutorMock.Verify(x => x.ExecuteAsync(
            "quote_get_state",
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    /// <summary>
    /// Verifies that token usage is summed across every iteration of the agent loop, not just the
    /// final call — otherwise the daily token budget (S2) would grossly undercount multi-call turns.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MultiIterationTurn_AccumulatesTokenUsageAcrossIterations()
    {
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Price this part" } }
        };

        var callCount = 0;
        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new GeminiResponse
                    {
                        Success = true,
                        ServiceTier = "flex",
                        TokenUsage = new GeminiTokenUsage
                        {
                            PromptTokens = 80,
                            CachedPromptTokens = 10,
                            ToolUsePromptTokens = 5,
                            ThoughtTokens = 7,
                            CompletionTokens = 20,
                            TotalTokens = 100
                        },
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall { Name = "quote_get_state", Args = new Dictionary<string, object>() }
                        },
                        GroundingWebSearchQueries = ["latest ASA material datasheet"]
                    };
                }

                return new GeminiResponse
                {
                    Success = true,
                    Content = "Here is your quote.",
                    ServiceTier = "flex",
                    TokenUsage = new GeminiTokenUsage
                    {
                        PromptTokens = 120,
                        CachedPromptTokens = 15,
                        ToolUsePromptTokens = 6,
                        ThoughtTokens = 8,
                        CompletionTokens = 30,
                        TotalTokens = 150
                    },
                    GroundingWebSearchQueries = ["official ASTM D638 source"]
                };
            });

        _toolExecutorMock.Setup(x => x.ExecuteAsync(
                "quote_get_state",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.True(result.Success);
        Assert.NotNull(result.TokenUsage);
        Assert.Equal(250, result.TokenUsage!.TotalTokens);
        Assert.Equal(200, result.TokenUsage.PromptTokens);
        Assert.Equal(25, result.TokenUsage.CachedPromptTokens);
        Assert.Equal(11, result.TokenUsage.ToolUsePromptTokens);
        Assert.Equal(15, result.TokenUsage.ThoughtTokens);
        Assert.Equal(50, result.TokenUsage.CompletionTokens);
        Assert.Equal("flex", result.ServiceTier);
        Assert.Equal(
            ["latest ASA material datasheet", "official ASTM D638 source"],
            result.GroundingWebSearchQueries);
    }

    /// <summary>
    /// Verifies that when the provider reports no token usage, the accumulated usage stays null rather
    /// than collapsing to a zero-valued object.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NoProviderUsage_LeavesTokenUsageNull()
    {
        var initialRequest = new GeminiRequest
        {
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Hello" } }
        };

        _geminiClientMock.Setup(x => x.SendMessageAsync(It.IsAny<GeminiRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeminiResponse { Success = true, Content = "Hi there." });

        var result = await _handler.ExecuteAsync(initialRequest);

        Assert.True(result.Success);
        Assert.Null(result.TokenUsage);
    }

    private static async IAsyncEnumerable<GeminiStreamEvent> CreateStream(IEnumerable<GeminiStreamEvent> events)
    {
        foreach (var streamEvent in events)
        {
            yield return streamEvent;
            await Task.Yield();
        }
    }
}
