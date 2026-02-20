using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Unit tests for ResponseTimeoutService.
/// </summary>
public class ResponseTimeoutServiceTests
{
    private readonly Mock<ILogger<ResponseTimeoutService>> _loggerMock;
    private readonly ResponseTimeoutService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseTimeoutServiceTests"/> class.
    /// </summary>
    public ResponseTimeoutServiceTests()
    {
        _loggerMock = new Mock<ILogger<ResponseTimeoutService>>();
        _service = new ResponseTimeoutService(_loggerMock.Object);
    }

    /// <summary>
    /// Tests that ExecuteWithTimeoutAsync returns the result if it completes within timeout.
    /// </summary>
    [Fact]
    public async Task ExecuteWithTimeoutAsync_CompletesWithinTimeout_ReturnsResult()
    {
        // Arrange
        var expected = "success";

        // Act
        var result = await _service.ExecuteWithTimeoutAsync(
            async (ct) => { await Task.Delay(10, ct); return expected; },
            1);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that ExecuteWithTimeoutAsync returns default/null if it times out.
    /// </summary>
    [Fact]
    public async Task ExecuteWithTimeoutAsync_TimesOut_ReturnsDefault()
    {
        // Act
        var result = await _service.ExecuteWithTimeoutAsync(
            async (ct) => { await Task.Delay(2000, ct); return "fail"; },
            1);

        // Assert
        Assert.Null(result);
    }
}
