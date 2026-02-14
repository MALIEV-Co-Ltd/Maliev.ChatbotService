using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Unit tests for WebSearchService.
/// </summary>
public class WebSearchServiceTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<WebSearchService>> _loggerMock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchServiceTests"/> class.
    /// </summary>
    public WebSearchServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<WebSearchService>>();
        
        _configurationMock.Setup(c => c["GoogleCustomSearch:ApiKey"]).Returns("test-key");
        _configurationMock.Setup(c => c["GoogleCustomSearch:SearchEngineId"]).Returns("test-cx");
    }

    /// <summary>
    /// Tests that SearchAsync returns results on success.
    /// </summary>
    [Fact]
    public async Task SearchAsync_Success_ReturnsResults()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new
                {
                    items = new[]
                    {
                        new { title = "Result 1", link = "https://example.com/1", snippet = "Snippet 1" }
                    }
                })
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new WebSearchService(httpClient, _configurationMock.Object, _loggerMock.Object);

        // Act
        var results = await service.SearchAsync("test query");

        // Assert
        Assert.Single(results);
        Assert.Equal("Result 1", results[0].Title);
        Assert.Equal("example.com", results[0].Domain);
    }

    /// <summary>
    /// Tests that SearchAsync returns empty results when API is not configured.
    /// </summary>
    [Fact]
    public async Task SearchAsync_NotConfigured_ReturnsEmpty()
    {
        // Arrange
        _configurationMock.Setup(c => c["GoogleCustomSearch:ApiKey"]).Returns("");
        var service = new WebSearchService(new HttpClient(), _configurationMock.Object, _loggerMock.Object);

        // Act
        var results = await service.SearchAsync("test query");

        // Assert
        Assert.Empty(results);
    }
}
