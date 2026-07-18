namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Receipt microservice operations.
/// </summary>
public class ReceiptToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "ReceiptService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, string? userToken, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_receipts" => await GetAsync($"/receipt/v1/receipts?page={GetIntArg(args, "page")}&pageSize=10", userToken, cancellationToken),
            "get_receipt" => await GetAsync($"/receipt/v1/receipts/{GetStringArg(args, "receipt_id")}", userToken, cancellationToken),
            _ => """{"error": "Unknown receipt tool"}"""
        };
    }
}
