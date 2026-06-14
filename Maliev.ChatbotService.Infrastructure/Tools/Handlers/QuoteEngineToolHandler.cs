using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Forwards customer-safe QuoteEngine agent tools to the QuoteEngine BFF.
/// </summary>
public sealed class QuoteEngineToolHandler(IHttpClientFactory httpClientFactory) : IContextualToolHandler
{
    private const string AgentContextHeader = "X-Maliev-Agent-Context";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly HashSet<string> AllowedTools = new(StringComparer.Ordinal)
    {
        "quote_get_state",
        "quote_get_project_summary",
        "quote_get_connectors",
        "quote_register_uploads",
        "quote_resume_project",
        "quote_search_customer_data",
        "quote_get_auth_handoff",
        "quote_get_settings",
        "quote_update_settings",
        "quote_get_reference_data",
        "quote_update_part_configuration",
        "quote_calculate_estimate",
        "quote_update_checkout_details",
        "quote_prepare_draft_project",
        "quote_duplicate_project",
        "quote_pin_project",
        "quote_unpin_project",
        "quote_archive_project",
        "quote_prepare_formal_quote",
        "quote_approve_quote",
        "quote_acknowledge_dfm",
        "quote_create_order",
        "quote_start_payment",
        "quote_get_account_context"
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(
        string toolName,
        Dictionary<string, object> args,
        string? userToken,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(toolName, args, new ToolExecutionContext(userToken, null), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ExecuteAsync(
        string toolName,
        Dictionary<string, object> args,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!AllowedTools.Contains(toolName))
        {
            return JsonSerializer.Serialize(new { error = $"Unknown QuoteEngine tool: {toolName}" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(context.QuoteAgentContextToken))
        {
            return JsonSerializer.Serialize(new { error = "QuoteEngine agent context is required." }, JsonOptions);
        }

        var client = httpClientFactory.CreateClient("QuoteEngineBff");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/quote/v1/agent/tools/{Uri.EscapeDataString(toolName)}")
        {
            Content = JsonContent.Create(new QuoteEngineToolRequest(args), options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation(AgentContextHeader, context.QuoteAgentContextToken);

        if (!string.IsNullOrWhiteSpace(context.UserToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.UserToken);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"QuoteEngine BFF returned {(int)response.StatusCode}",
                detail = content
            }, JsonOptions);
        }

        return content;
    }

    private sealed record QuoteEngineToolRequest(Dictionary<string, object> Arguments);
}
