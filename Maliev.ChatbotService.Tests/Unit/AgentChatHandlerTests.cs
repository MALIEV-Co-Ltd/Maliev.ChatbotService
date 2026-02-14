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

        var pdfData = "JVBERi0xLjQKJ..." ; // Mock base64
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

        _toolExecutorMock.Setup(x => x.ExecuteAsync("get_document_content", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
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

        _toolExecutorMock.Setup(x => x.ExecuteAsync("get_customer", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        // Act
        var result = await _handler.ExecuteAsync(initialRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, capturedRequests.Count);
        
        // Verify that no attachments were added in either call
        Assert.All(capturedRequests, r => Assert.All(r.Messages, m => Assert.Null(m.Attachments)));
    }
}
