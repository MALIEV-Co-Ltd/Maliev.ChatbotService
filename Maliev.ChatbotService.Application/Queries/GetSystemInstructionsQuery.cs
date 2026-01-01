namespace Maliev.ChatbotService.Application.Queries;

/// <summary>
/// Query to get system instructions.
/// </summary>
public class GetSystemInstructionsQuery
{
    /// <summary>
    /// Gets or sets a value indicating whether to return only active instructions.
    /// </summary>
    public bool ActiveOnly { get; set; }
}
