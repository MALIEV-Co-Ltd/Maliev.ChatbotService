namespace Maliev.ChatbotService.Application.Queries;

/// <summary>
/// Query to get user preferences with pagination.
/// </summary>
public class GetUserPreferencesQuery
{
    /// <summary>
    /// Gets or sets the user profile ID.
    /// </summary>
    public Guid UserProfileId { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 20;
}
