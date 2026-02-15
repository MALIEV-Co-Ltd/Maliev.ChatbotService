namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Employee microservice operations.
/// </summary>
public class EmployeeToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "EmployeeService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_employees" => await GetAsync($"/employee/v1/employees?query={Uri.EscapeDataString(GetStringArg(args, "query"))}&page={GetIntArg(args, "page")}&pageSize=10", cancellationToken),
            "get_employee" => await GetAsync($"/employee/v1/employees/{GetStringArg(args, "employee_id")}", cancellationToken),
            _ => """{"error": "Unknown employee tool"}"""
        };
    }
}
