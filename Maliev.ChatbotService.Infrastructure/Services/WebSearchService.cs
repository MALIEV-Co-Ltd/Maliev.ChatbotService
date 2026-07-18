using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for web search operations using Google Custom Search API.
/// </summary>
public class WebSearchService : IWebSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebSearchService> _logger;
    private readonly string _apiKey;
    private readonly string _searchEngineId;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSearchService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public WebSearchService(HttpClient httpClient, IConfiguration configuration, ILogger<WebSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["GoogleCustomSearch:ApiKey"] ?? string.Empty;
        _searchEngineId = configuration["GoogleCustomSearch:SearchEngineId"] ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_searchEngineId))
        {
            _logger.LogWarning("Google Custom Search API not configured");
            return new List<SearchResult>();
        }

        try
        {
            var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Custom Search API returned error: {StatusCode}", response.StatusCode);
                return new List<SearchResult>();
            }

            var jsonDoc = JsonDocument.Parse(content);
            var results = new List<SearchResult>();

            if (jsonDoc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var title = item.GetProperty("title").GetString() ?? string.Empty;
                    var link = item.GetProperty("link").GetString() ?? string.Empty;
                    var snippet = item.GetProperty("snippet").GetString() ?? string.Empty;

                    var domain = string.Empty;
                    if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
                    {
                        domain = uri.Host;
                    }

                    results.Add(new SearchResult
                    {
                        Title = title,
                        Url = link,
                        Snippet = snippet,
                        Domain = domain
                    });
                }
            }

            _logger.LogInformation("Retrieved {Count} search results for query: {Query}", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing web search for query: {Query}", query);
            return new List<SearchResult>();
        }
    }
}
