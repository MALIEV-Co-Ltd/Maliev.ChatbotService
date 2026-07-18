namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response model for customer intent extraction.
/// </summary>
public class ExtractCustomerIntentResponse
{
    /// <summary>
    /// Gets or sets whether the user needs customer data.
    /// </summary>
    public bool NeedsCustomerData { get; set; }

    /// <summary>
    /// Gets or sets the customer search term extracted from the message.
    /// </summary>
    public string? CustomerSearchTerm { get; set; }

    /// <summary>
    /// Gets or sets whether the user needs activity/audit history.
    /// </summary>
    public bool NeedsHistory { get; set; }
}
