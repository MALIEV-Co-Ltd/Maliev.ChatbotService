using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for executing CRM operations (quotations, orders, customer updates).
/// </summary>
public class OperationExecutionService : IOperationExecutionService
{
    private readonly HttpClient _quotationServiceClient;
    private readonly HttpClient _orderServiceClient;
    private readonly HttpClient _customerServiceClient;
    private readonly ILogger<OperationExecutionService> _logger;
    private readonly IOperationLogRepository _operationLogRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationExecutionService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="operationLogRepository">The operation log repository.</param>
    public OperationExecutionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OperationExecutionService> logger,
        IOperationLogRepository operationLogRepository)
    {
        _quotationServiceClient = httpClientFactory.CreateClient("QuotationService");
        _quotationServiceClient.BaseAddress = new Uri(configuration["ExternalServices:QuotationService:BaseUrl"] ?? "http://maliev-quotation-service:8080");

        _orderServiceClient = httpClientFactory.CreateClient("OrderService");
        _orderServiceClient.BaseAddress = new Uri(configuration["ExternalServices:OrderService:BaseUrl"] ?? "http://maliev-order-service:8080");

        _customerServiceClient = httpClientFactory.CreateClient("CustomerService");
        _customerServiceClient.BaseAddress = new Uri(configuration["ExternalServices:CustomerService:BaseUrl"] ?? "http://maliev-customer-service:8080");

        _logger = logger;
        _operationLogRepository = operationLogRepository;
    }

    /// <inheritdoc/>
    public async Task<OperationResult> ExecuteOperationAsync(string operationType, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing operation {OperationType} with parameters {Parameters}", operationType, JsonSerializer.Serialize(parameters));

        try
        {
            return operationType.ToLowerInvariant() switch
            {
                "quotationquery" => await ExecuteQuotationQueryAsync(parameters, cancellationToken),
                "orderstatusquery" => await ExecuteOrderStatusQueryAsync(parameters, cancellationToken),
                "crmupdate" => await ExecuteCRMUpdateAsync(parameters, cancellationToken),
                "sendreminder" => await ExecuteSendReminderAsync(parameters, cancellationToken),
                _ => new OperationResult
                {
                    Success = false,
                    ErrorMessage = $"Unknown operation type: {operationType}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation {OperationType}", operationType);
            return new OperationResult
            {
                Success = false,
                ErrorMessage = $"Operation failed: {ex.Message}"
            };
        }
    }

    private async Task<OperationResult> ExecuteQuotationQueryAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("quotationId", out var quotationIdObj))
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Missing quotationId parameter"
            };
        }

        var quotationId = quotationIdObj.ToString();
        _logger.LogInformation("Querying quotation {QuotationId}", quotationId);

        try
        {
            // Mock response for testing - in production, call actual QuotationService API
            var mockQuotationData = new
            {
                QuotationId = quotationId,
                CustomerName = "ABC Manufacturing Co.",
                Status = "Pending Customer Approval",
                TotalAmount = 125000.00m,
                Currency = "THB",
                ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
                Items = new[]
                {
                    new { Description = "CNC Machined Parts - 100 units", UnitPrice = 1250.00m }
                }
            };

            var formattedMessage = $@"Quotation {quotationId}:
Customer: {mockQuotationData.CustomerName}
Status: {mockQuotationData.Status}
Total: {mockQuotationData.TotalAmount:N2} {mockQuotationData.Currency}
Valid Until: {mockQuotationData.ValidUntil:yyyy-MM-dd}";

            var result = new OperationResult
            {
                Success = true,
                Data = mockQuotationData,
                FormattedMessage = formattedMessage,
                SuggestedActions = new List<OperationAction>
                {
                    new()
                    {
                        Label = "Send Reminder",
                        ActionType = "SendReminder",
                        Parameters = new Dictionary<string, object> { { "quotationId", quotationId! } }
                    },
                    new()
                    {
                        Label = "View Full Details",
                        ActionType = "ViewDetails",
                        Parameters = new Dictionary<string, object> { { "quotationId", quotationId! } }
                    }
                }
            };

            // Log operation
            await LogOperationAsync("QuotationQuery", parameters, result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying quotation {QuotationId}", quotationId);
            return new OperationResult
            {
                Success = false,
                ErrorMessage = $"Failed to query quotation: {ex.Message}"
            };
        }
    }

    private async Task<OperationResult> ExecuteOrderStatusQueryAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("orderId", out var orderIdObj))
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Missing orderId parameter"
            };
        }

        var orderId = orderIdObj.ToString();
        _logger.LogInformation("Querying order {OrderId}", orderId);

        try
        {
            // Mock response for testing - in production, call actual OrderService API
            var mockOrderData = new
            {
                OrderId = orderId,
                CustomerName = "XYZ Industries Ltd.",
                Status = "InProduction",
                OrderDate = DateTimeOffset.UtcNow.AddDays(-10),
                ExpectedDelivery = DateTimeOffset.UtcNow.AddDays(5),
                TotalAmount = 285000.00m,
                Currency = "THB"
            };

            var formattedMessage = $@"Order {orderId}:
Customer: {mockOrderData.CustomerName}
Status: {mockOrderData.Status}
Order Date: {mockOrderData.OrderDate:yyyy-MM-dd}
Expected Delivery: {mockOrderData.ExpectedDelivery:yyyy-MM-dd}
Total: {mockOrderData.TotalAmount:N2} {mockOrderData.Currency}";

            var result = new OperationResult
            {
                Success = true,
                Data = mockOrderData,
                FormattedMessage = formattedMessage,
                SuggestedActions = new List<OperationAction>
                {
                    new()
                    {
                        Label = "Update Status",
                        ActionType = "UpdateStatus",
                        Parameters = new Dictionary<string, object> { { "orderId", orderId! } }
                    }
                }
            };

            // Log operation
            await LogOperationAsync("OrderStatusQuery", parameters, result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying order {OrderId}", orderId);
            return new OperationResult
            {
                Success = false,
                ErrorMessage = $"Failed to query order: {ex.Message}"
            };
        }
    }

    private async Task<OperationResult> ExecuteCRMUpdateAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing CRM update");

        // Mock CRM update
        var result = new OperationResult
        {
            Success = true,
            FormattedMessage = "CRM record updated successfully",
            Data = new { UpdatedAt = DateTimeOffset.UtcNow }
        };

        await LogOperationAsync("CRMUpdate", parameters, result, cancellationToken);

        return result;
    }

    private async Task<OperationResult> ExecuteSendReminderAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("quotationId", out var quotationIdObj))
        {
            return new OperationResult
            {
                Success = false,
                ErrorMessage = "Missing quotationId parameter"
            };
        }

        var quotationId = quotationIdObj.ToString();
        _logger.LogInformation("Sending reminder for quotation {QuotationId}", quotationId);

        // Mock reminder sending
        var result = new OperationResult
        {
            Success = true,
            FormattedMessage = $"Reminder sent successfully for quotation {quotationId}",
            Data = new { SentAt = DateTimeOffset.UtcNow, QuotationId = quotationId }
        };

        await LogOperationAsync("SendReminder", parameters, result, cancellationToken);

        return result;
    }

    private async Task LogOperationAsync(string operationType, Dictionary<string, object> parameters, OperationResult result, CancellationToken cancellationToken)
    {
        try
        {
            var operationLog = new Domain.Entities.OperationLog
            {
                Id = Guid.NewGuid(),
                OperationType = operationType,
                OperationParameters = JsonSerializer.Serialize(parameters),
                ExecutionResult = JsonSerializer.Serialize(result.Data),
                ExecutedAt = DateTimeOffset.UtcNow,
                Success = result.Success
            };

            await _operationLogRepository.CreateAsync(operationLog, cancellationToken);
            _logger.LogDebug("Logged operation {OperationType} with result {Success}", operationType, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log operation {OperationType}", operationType);
        }
    }
}
