using System.Net;
using System.Text.Json;
using Maliev.ChatbotService.Infrastructure.Tools.Handlers;
using Moq;
using Moq.Protected;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Unit tests for DocumentToolHandler.
/// </summary>
public class DocumentToolHandlerTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly DocumentToolHandler _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentToolHandlerTests"/> class.
    /// </summary>
    public DocumentToolHandlerTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        var client = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://test.com")
        };

        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
        _handler = new DocumentToolHandler(_httpClientFactoryMock.Object);
    }

    /// <summary>
    /// Verifies that GetDocumentContentAsync returns the PDF data as base64 in the metadata property.
    /// </summary>
    [Fact]
    public async Task GetDocumentContentAsync_ReturnsBase64Metadata()
    {
        // Arrange
        var args = new Dictionary<string, object>
        {
            ["file_reference"] = "files/test.pdf"
        };

        // 1. Mock Signed URL response from UploadService
        var signedUrlResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { url = "http://download.com/test.pdf" }))
        };

        // 2. Mock Download response
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00 }; // Mock PDF header
        var downloadResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(pdfBytes)
        };

        _httpMessageHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(signedUrlResponse)
            .ReturnsAsync(downloadResponse);

        // Act
        var resultJson = await _handler.ExecuteAsync("get_document_content", args, CancellationToken.None);
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson);

        // Assert
        Assert.True(result.TryGetProperty("_metadata", out var metadata));
        Assert.Equal("application/pdf", metadata.GetProperty("mime_type").GetString());
        Assert.Equal(Convert.ToBase64String(pdfBytes), metadata.GetProperty("data").GetString());
        Assert.True(metadata.GetProperty("is_file").GetBoolean());
    }
}
