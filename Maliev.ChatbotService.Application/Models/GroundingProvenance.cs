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
    /// Gets or sets the SHA-256 digest of the normalized customer address input validated by this result.
    /// </summary>
    public string? AddressDigest { get; set; }

    /// <summary>
    /// Gets or sets normalized source-backed Thai administrative address evidence for shipping turns.
    /// </summary>
    public GroundedShippingAddressEvidence? ShippingAddress { get; set; }

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

/// <summary>
/// Normalized administrative address fields derived from grounded public sources.
/// </summary>
public sealed class GroundedShippingAddressEvidence
{
    /// <summary>Gets or sets the normalized subdistrict comparison key.</summary>
    public string Subdistrict { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized district comparison key.</summary>
    public string District { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized province comparison key.</summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>Gets or sets the five-digit Thai postcode.</summary>
    public string Postcode { get; set; } = string.Empty;
}
