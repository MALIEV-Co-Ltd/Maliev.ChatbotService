using Maliev.ChatbotService.Application.Models;

namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Represents a paginated response for user preferences.
/// </summary>
public class PagedPreferencesResponse
{
    /// <summary>
    /// Gets or sets the list of user preferences.
    /// </summary>
    public List<ExtractedPreference> Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the pagination metadata.
    /// </summary>
    public PaginationMeta Meta { get; set; } = new();
}

/// <summary>
/// Represents pagination metadata.
/// </summary>
public class PaginationMeta
{
    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total count of items.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
