namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Material microservice operations.
/// </summary>
public class MaterialToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "MaterialService";

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_materials" => await GetAsync($"/material/v1/materials?query={Uri.EscapeDataString(GetStringArg(args, "query"))}&page={GetIntArg(args, "page")}&pageSize=10", cancellationToken),
            "get_material" => await GetAsync($"/material/v1/materials/{GetStringArg(args, "material_id")}", cancellationToken),
            _ => """{"error": "Unknown material tool"}"""
        };
    }
}
