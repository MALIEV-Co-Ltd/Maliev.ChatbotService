namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Order microservice operations.
/// </summary>
public class OrderToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "OrderService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_orders" => await SearchOrders(args, cancellationToken),
            "get_order" => await GetAsync($"/order/v1/orders/{GetStringArg(args, "order_id")}", cancellationToken),
            _ => """{"error": "Unknown order tool"}"""
        };
    }

    private async Task<string> SearchOrders(Dictionary<string, object> args, CancellationToken ct)
    {
        var query = $"/order/v1/orders?page={GetIntArg(args, "page")}&pageSize=10";
        var customerId = GetStringArg(args, "customer_id");
        if (!string.IsNullOrEmpty(customerId)) query += $"&customerId={customerId}";
        var status = GetStringArg(args, "status");
        if (!string.IsNullOrEmpty(status)) query += $"&status={Uri.EscapeDataString(status)}";
        return await GetAsync(query, ct);
    }
}
