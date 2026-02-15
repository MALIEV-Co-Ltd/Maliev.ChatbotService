namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Quotation microservice operations.
/// </summary>
public class QuotationToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "QuotationService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_quotations" => await SearchQuotations(args, cancellationToken),
            "get_quotation" => await GetAsync($"/quotation/v1/quotations/{GetStringArg(args, "quotation_id")}", cancellationToken),
            _ => """{"error": "Unknown quotation tool"}"""
        };
    }

    private async Task<string> SearchQuotations(Dictionary<string, object> args, CancellationToken ct)
    {
        var query = $"/quotation/v1/quotations?page={GetIntArg(args, "page")}&pageSize=10";
        var customerId = GetStringArg(args, "customer_id");
        if (!string.IsNullOrEmpty(customerId)) query += $"&customerId={customerId}";
        var status = GetStringArg(args, "status");
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        return await GetAsync(query, ct);
    }
}
