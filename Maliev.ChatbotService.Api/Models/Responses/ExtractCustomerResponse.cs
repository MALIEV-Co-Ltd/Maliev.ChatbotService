using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>
/// Response model containing customer data extracted by AI.
/// </summary>
public class ExtractCustomerResponse
{
    /// <summary>Gets or sets the extracted first name.</summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the extracted last name.</summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    /// <summary>Gets or sets the extracted email address.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Gets or sets the extracted mobile phone number.</summary>
    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    /// <summary>Gets or sets the extracted landline phone number.</summary>
    [JsonPropertyName("landline")]
    public string? Landline { get; set; }

    /// <summary>Gets or sets the extracted phone extension.</summary>
    [JsonPropertyName("extension")]
    public string? Extension { get; set; }

    /// <summary>Gets or sets the extracted customer segment.</summary>
    [JsonPropertyName("segment")]
    public string? Segment { get; set; }

    /// <summary>Gets or sets the extracted company name.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    /// <summary>Gets or sets the extracted company phone number.</summary>
    [JsonPropertyName("company_phone")]
    public string? CompanyPhone { get; set; }

    /// <summary>Gets or sets the extracted VAT/tax number.</summary>
    [JsonPropertyName("vat_number")]
    public string? VatNumber { get; set; }

    /// <summary>Gets or sets the extracted branch number (สาขาที่).</summary>
    [JsonPropertyName("branch_number")]
    public string? BranchNumber { get; set; }

    /// <summary>Gets or sets the extracted addresses.</summary>
    [JsonPropertyName("addresses")]
    public List<ExtractedAddressDto>? Addresses { get; set; }
}

/// <summary>
/// An address extracted by AI with structured fields.
/// </summary>
public class ExtractedAddressDto
{
    /// <summary>Gets or sets the address type (Billing or Shipping).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the first address line.</summary>
    [JsonPropertyName("address_line_1")]
    public string? AddressLine1 { get; set; }

    /// <summary>Gets or sets the second address line.</summary>
    [JsonPropertyName("address_line_2")]
    public string? AddressLine2 { get; set; }

    /// <summary>Gets or sets the third address line.</summary>
    [JsonPropertyName("address_line_3")]
    public string? AddressLine3 { get; set; }

    /// <summary>Gets or sets the sub-district.</summary>
    [JsonPropertyName("district")]
    public string? District { get; set; }

    /// <summary>Gets or sets the district/city.</summary>
    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>Gets or sets the province.</summary>
    [JsonPropertyName("state_province")]
    public string? StateProvince { get; set; }

    /// <summary>Gets or sets the postal code.</summary>
    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    /// <summary>Gets or sets the shipping recipient name.</summary>
    [JsonPropertyName("recipient_name")]
    public string? RecipientName { get; set; }

    /// <summary>Gets or sets the shipping recipient phone.</summary>
    [JsonPropertyName("recipient_phone")]
    public string? RecipientPhone { get; set; }
}

