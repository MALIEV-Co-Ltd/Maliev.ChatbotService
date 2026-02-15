namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Payment microservice operations.
/// </summary>
public class PaymentToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "PaymentService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_payments" => await GetAsync($"/payment/v1/payments?page={GetIntArg(args, "page")}&pageSize=10", cancellationToken),
            "get_payment" => await GetAsync($"/payment/v1/payments/{GetStringArg(args, "payment_id")}", cancellationToken),
            _ => """{"error": "Unknown payment tool"}"""
        };
    }
}
