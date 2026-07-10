using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Application.Models;

/// <summary>
/// Customer-safe provenance for a turn that required external web grounding.
/// </summary>
public sealed class GroundingProvenance
{
    /// <summary>
    /// Gets or sets the bounded grounding purpose identifier.
    /// </summary>
    public string Purpose { get; set; } = "web_search";

    /// <summary>
    /// Gets or sets the external grounding provider identifier.
    /// </summary>
    public string Provider { get; set; } = "google_search";

    /// <summary>
    /// Gets or sets the grounding result: grounded, no_evidence, or unavailable.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bounded provider-reported search queries.
    /// </summary>
    public List<string> Queries { get; set; } = [];

    /// <summary>
    /// Gets or sets the bounded HTTPS sources used by the grounded turn.
    /// </summary>
    public List<GeminiGroundingSource> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets a customer-safe error code when grounding did not succeed.
    /// </summary>
    public string? ErrorCode { get; set; }
}
