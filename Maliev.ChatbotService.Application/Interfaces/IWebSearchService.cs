namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for web search operations using Google Custom Search API.
/// </summary>
public interface IWebSearchService
{
    /// <summary>
    /// Performs a web search using Google Custom Search API.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results as a list of search result items.</returns>
    Task<List<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single search result.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Gets or sets the title of the search result.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the search result.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the snippet/description of the search result.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the domain of the search result.
    /// </summary>
    public string Domain { get; set; } = string.Empty;
}
