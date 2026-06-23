using System.Text.Json;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
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
            Messages = new List<GeminiMessage> { new GeminiMessage { Role = "user", Content = "Read this NDA" } }
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
                        TokenUsage = new GeminiTokenUsage { PromptTokens = 80, CompletionTokens = 20, TotalTokens = 100 },
                        FunctionCalls = new List<GeminiFunctionCall>
                        {
                            new GeminiFunctionCall { Name = "quote_get_state", Args = new Dictionary<string, object>() }
                        }
                    };
                }

                return new GeminiResponse
                {
                    Success = true,
                    Content = "Here is your quote.",
                    TokenUsage = new GeminiTokenUsage { PromptTokens = 120, CompletionTokens = 30, TotalTokens = 150 }
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
        Assert.Equal(50, result.TokenUsage.CompletionTokens);
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
