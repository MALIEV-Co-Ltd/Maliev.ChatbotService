namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Supplier microservice operations.
/// </summary>
public class SupplierToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "SupplierService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, string? userToken, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_suppliers" => await GetAsync($"/supplier/v1/suppliers?query={Uri.EscapeDataString(GetStringArg(args, "query"))}&page={GetIntArg(args, "page")}&pageSize=10", userToken, cancellationToken),
            "get_supplier" => await GetAsync($"/supplier/v1/suppliers/{GetStringArg(args, "supplier_id")}", userToken, cancellationToken),
            _ => """{"error": "Unknown supplier tool"}"""
        };
    }
}
