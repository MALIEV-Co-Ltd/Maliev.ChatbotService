using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Queries;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for getting user preferences with pagination.
/// </summary>
public class GetUserPreferencesQueryHandler
{
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly ILogger<GetUserPreferencesQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserPreferencesQueryHandler"/> class.
    /// </summary>
    /// <param name="userMemoryRepository">The user memory repository.</param>
    /// <param name="logger">The logger.</param>
    public GetUserPreferencesQueryHandler(
        IUserMemoryRepository userMemoryRepository,
        ILogger<GetUserPreferencesQueryHandler> logger)
    {
        _userMemoryRepository = userMemoryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the GetUserPreferencesQuery.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The paginated preferences result.</returns>
    public async Task<GetUserPreferencesResult> HandleAsync(GetUserPreferencesQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching preferences for user {UserProfileId}, page {Page}, size {PageSize}",
            query.UserProfileId, query.Page, query.PageSize);

        var (memories, totalCount) = await _userMemoryRepository.GetByUserIdAsync(
            query.UserProfileId,
            query.Page,
            query.PageSize,
            cancellationToken);

        _logger.LogInformation("Retrieved {Count} preferences out of {TotalCount} for user {UserProfileId}",
            memories.Count, totalCount, query.UserProfileId);

        return new GetUserPreferencesResult
        {
            Preferences = memories,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}

/// <summary>
/// Result of getting user preferences.
/// </summary>
public class GetUserPreferencesResult
{
    /// <summary>
    /// Gets or sets the list of user preferences.
    /// </summary>
    public List<UserMemory> Preferences { get; set; } = new();

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total count of preferences.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
