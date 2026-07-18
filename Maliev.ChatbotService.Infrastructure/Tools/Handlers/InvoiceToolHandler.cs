namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Invoice microservice operations.
/// </summary>
public class InvoiceToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "InvoiceService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, string? userToken, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_invoices" => await SearchInvoices(args, userToken, cancellationToken),
            "get_invoice" => await GetAsync($"/invoice/v1/invoices/{GetStringArg(args, "invoice_id")}", userToken, cancellationToken),
            _ => """{"error": "Unknown invoice tool"}"""
        };
    }

    private async Task<string> SearchInvoices(Dictionary<string, object> args, string? userToken, CancellationToken ct)
    {
        var query = $"/invoice/v1/invoices?page={GetIntArg(args, "page")}&pageSize=10";
        var customerId = GetStringArg(args, "customer_id");
        if (!string.IsNullOrEmpty(customerId)) query += $"&customerId={customerId}";
        var status = GetStringArg(args, "status");
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        return await GetAsync(query, userToken, ct);
    }
}
