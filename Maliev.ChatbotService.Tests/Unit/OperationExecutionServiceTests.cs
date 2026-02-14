using Maliev.ChatbotService.Application.Interfaces;
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
/// Unit tests for OperationExecutionService.
/// </summary>
public class OperationExecutionServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IOperationLogRepository> _operationLogRepositoryMock;
    private readonly Mock<ILogger<OperationExecutionService>> _loggerMock;
    private readonly OperationExecutionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationExecutionServiceTests"/> class.
    /// </summary>
    public OperationExecutionServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _operationLogRepositoryMock = new Mock<IOperationLogRepository>();
        _loggerMock = new Mock<ILogger<OperationExecutionService>>();
        
        // Setup default configuration values
        _configurationMock.Setup(c => c["ExternalServices:QuotationService:BaseUrl"]).Returns("http://quotationservice");
        _configurationMock.Setup(c => c["ExternalServices:OrderService:BaseUrl"]).Returns("http://orderservice");
        _configurationMock.Setup(c => c["ExternalServices:CustomerService:BaseUrl"]).Returns("http://customerservice");

        // Setup mock clients for constructor
        var mockHttpClient = new HttpClient(new Mock<HttpMessageHandler>().Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(mockHttpClient);

        _service = new OperationExecutionService(
            _httpClientFactoryMock.Object, 
            _configurationMock.Object,
            _loggerMock.Object,
            _operationLogRepositoryMock.Object);
    }

    /// <summary>
    /// Tests that ExecuteQuotationQueryAsync returns a formatted message on success.
    /// </summary>
    [Fact]
    public async Task ExecuteQuotationQueryAsync_Success_ReturnsFormattedMessage()
    {
        // Arrange
        var quotationId = "Q-2025-001";
        var parameters = new Dictionary<string, object> { { "quotationId", quotationId } };
        
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
                    quotation_id = quotationId,
                    status = "Approved",
                    total_amount = 1500.50,
                    currency = "USD"
                })
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://quotationservice") };
        _httpClientFactoryMock.Setup(f => f.CreateClient("QuotationService")).Returns(httpClient);

        // Act
        var result = await _service.ExecuteOperationAsync("QuotationQuery", parameters);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(quotationId, result.FormattedMessage);
        Assert.Contains("Pending Customer Approval", result.FormattedMessage);
    }

    /// <summary>
    /// Tests that ExecuteOrderStatusQueryAsync returns a formatted message on success.
    /// </summary>
    [Fact]
    public async Task ExecuteOrderStatusQueryAsync_Success_ReturnsFormattedMessage()
    {
        // Arrange
        var orderId = "O-2025-001";
        var parameters = new Dictionary<string, object> { { "orderId", orderId } };
        
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
                    order_id = orderId,
                    status = "InProduction",
                    tracking_number = "TRACK123"
                })
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orderservice") };
        _httpClientFactoryMock.Setup(f => f.CreateClient("OrderService")).Returns(httpClient);

        // Act
        var result = await _service.ExecuteOperationAsync("OrderStatusQuery", parameters);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(orderId, result.FormattedMessage);
        Assert.Contains("InProduction", result.FormattedMessage);
    }

    /// <summary>
    /// Tests that ExecuteOperationAsync returns failure for unknown operation types.
    /// </summary>
    [Fact]
    public async Task ExecuteOperationAsync_UnknownType_ReturnsFailure()
    {
        // Act
        var result = await _service.ExecuteOperationAsync("UnknownOp", new Dictionary<string, object>());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Unknown operation type", result.ErrorMessage);
    }
}
