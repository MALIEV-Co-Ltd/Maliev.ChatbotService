using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Customer microservice operations.
/// </summary>
public class CustomerToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "CustomerService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, string? userToken, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_customers" => await GetAsync($"/customer/v1/customers?query={Uri.EscapeDataString(GetStringArg(args, "query"))}&page={GetIntArg(args, "page")}&pageSize=10", userToken, cancellationToken),
            "get_customer" => await GetAsync($"/customer/v1/customers/{GetStringArg(args, "customer_id")}", userToken, cancellationToken),
            "get_customer_metrics" => await GetCustomerMetricsAsync(userToken, cancellationToken),
            "add_customer_address" => await AddCustomerAddressAsync(args, userToken, cancellationToken),
            _ => """{"error": "Unknown customer tool"}"""
        };
    }

    private async Task<string> GetCustomerMetricsAsync(string? userToken, CancellationToken cancellationToken)
    {
        // Get 1 item to minimize data transfer, we only need the totalCount from metadata
        var response = await GetAsync("/customer/v1/customers?pageSize=1", userToken, cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("totalCount", out var totalCount))
            {
                return JsonSerializer.Serialize(new { totalCustomers = totalCount.GetInt32() });
            }
        }
        catch
        {
            // If parsing fails or property doesn't exist, return original response or error
        }

        return response;
    }

    private async Task<string> AddCustomerAddressAsync(Dictionary<string, object> args, string? userToken, CancellationToken cancellationToken)
    {
        var payload = new
        {
            ownerType = "Customer",
            ownerId = GetStringArg(args, "customer_id"),
            label = GetStringArg(args, "label", "Shipping Address"),
            street = GetStringArg(args, "street"),
            city = GetStringArg(args, "city"),
            state = GetStringArg(args, "state"),
            postalCode = GetStringArg(args, "postal_code"),
            countryId = GetGuidArg(args, "country_id"),
            isDefaultBilling = GetBoolArg(args, "is_default_billing", false),
            isDefaultShipping = GetBoolArg(args, "is_default_shipping", true)
        };

        return await PostAsync("/customer/v1/addresses", payload, userToken, cancellationToken);
    }

    private static Guid? GetGuidArg(Dictionary<string, object> args, string key)
    {
        var val = GetStringArg(args, key);
        return Guid.TryParse(val, out var guid) ? guid : null;
    }

    private static bool GetBoolArg(Dictionary<string, object> args, string key, bool defaultValue)
    {
         if (args.TryGetValue(key, out var value))
         {
             if (value is bool b) return b;
             if (bool.TryParse(value?.ToString(), out var parsed)) return parsed;
         }
         return defaultValue;
    }
}
