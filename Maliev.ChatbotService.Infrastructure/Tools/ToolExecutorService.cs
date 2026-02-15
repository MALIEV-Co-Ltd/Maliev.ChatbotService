using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Tools.Handlers;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Tools;

/// <summary>
/// Routes tool/function calls to the appropriate handler and returns results.
/// </summary>
public class ToolExecutorService : IToolExecutorService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ToolExecutorService> _logger;
    private readonly Dictionary<string, IToolHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutorService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating named clients.</param>
    /// <param name="logger">The logger instance.</param>
    public ToolExecutorService(IHttpClientFactory httpClientFactory, ILogger<ToolExecutorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Register all handlers with their tool name prefixes
        var customerHandler = new CustomerToolHandler(httpClientFactory);
        var orderHandler = new OrderToolHandler(httpClientFactory);
        var quotationHandler = new QuotationToolHandler(httpClientFactory);
        var invoiceHandler = new InvoiceToolHandler(httpClientFactory);
        var paymentHandler = new PaymentToolHandler(httpClientFactory);
        var employeeHandler = new EmployeeToolHandler(httpClientFactory);
        var materialHandler = new MaterialToolHandler(httpClientFactory);
        var supplierHandler = new SupplierToolHandler(httpClientFactory);
        var receiptHandler = new ReceiptToolHandler(httpClientFactory);
        var documentHandler = new DocumentToolHandler(httpClientFactory);

        _handlers = new Dictionary<string, IToolHandler>
        {
            ["search_customers"] = customerHandler,
            ["get_customer"] = customerHandler,
            ["get_customer_metrics"] = customerHandler,
            ["add_customer_address"] = customerHandler,
            ["list_customer_ndas"] = documentHandler,
            ["get_document_content"] = documentHandler,
            ["search_orders"] = orderHandler,
            ["get_order"] = orderHandler,
            ["search_quotations"] = quotationHandler,
            ["get_quotation"] = quotationHandler,
            ["search_invoices"] = invoiceHandler,
            ["get_invoice"] = invoiceHandler,
            ["search_payments"] = paymentHandler,
            ["get_payment"] = paymentHandler,
            ["search_employees"] = employeeHandler,
            ["get_employee"] = employeeHandler,
            ["search_materials"] = materialHandler,
            ["get_material"] = materialHandler,
            ["search_suppliers"] = supplierHandler,
            ["get_supplier"] = supplierHandler,
            ["search_receipts"] = receiptHandler,
            ["get_receipt"] = receiptHandler
        };
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing tool: {ToolName} with args: {Args}", toolName, JsonSerializer.Serialize(args));

        if (!_handlers.TryGetValue(toolName, out var handler))
        {
            return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });
        }

        try
        {
            return await handler.ExecuteAsync(toolName, args, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = $"Error executing {toolName}: {ex.Message}" });
        }
    }

    /// <inheritdoc/>
    public List<GeminiToolDeclaration> GetToolDeclarations() => ToolRegistry.GetAllToolDeclarations();
}
