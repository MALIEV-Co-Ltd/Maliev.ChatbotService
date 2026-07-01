using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Application.Handlers;


/// <summary>
/// Handler for extracting customer data from documents or text using AI.
/// This bypasses the full conversation pipeline — it is a stateless extraction call.
/// </summary>
public class ExtractCustomerCommandHandler
{
    private const int MaxExtractionPromptTokens = 20000;
    private const int MaxExtractionOutputTokens = 4096;

    private readonly ISystemInstructionRepository _instructionRepository;
    private readonly IGeminiClient _geminiClient;
    private readonly IModelContextCacheService _modelContextCacheService;
    private readonly ILogger<ExtractCustomerCommandHandler> _logger;

    private static readonly object CustomerExtractionSchema = new
    {
        type = "object",
        properties = new
        {
            first_name = new { type = "string", description = "Person's first name (ชื่อ)" },
            last_name = new { type = "string", description = "Person's last name (นามสกุล)" },
            email = new { type = "string", description = "Email address" },
            mobile = new { type = "string", description = "Mobile phone number. Must include '+' prefix if country code is present, e.g. '+66898950690'." },
            landline = new { type = "string", description = "Landline/office phone number. Must include '+' prefix if country code is present." },
            extension = new { type = "string", description = "Phone extension number (extension for company landline)" },
            segment = new { type = "string", description = "Customer segment", @enum = new[] { "Retail", "Wholesale", "Enterprise", "Government" } },
            company_name = new { type = "string", description = "Company or business name (บริษัท, หจก.)" },
            company_phone = new { type = "string", description = "Company phone number" },
            vat_number = new { type = "string", description = "Tax ID / VAT number (เลขประจำตัวผู้เสียภาษี), numeric digits only" },
            branch_number = new { type = "string", description = "Branch number (สาขาที่) as 5-digit string (e.g. '00001'). If 'Head Office' or 'สำนักงานใหญ่' is mentioned, use '00000'." },
            addresses = new
            {
                type = "array",
                description = "List of extracted addresses",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        type = new { type = "string", description = "Address type", @enum = new[] { "Billing", "Shipping" } },
                        address_line_1 = new { type = "string", description = "Street-level address: house number, moo/หมู่, village/หมู่บ้าน, soi/ซอย, road/ถนน. Everything BEFORE the sub-district. e.g. '36/1 หมู่ 3'" },
                        address_line_2 = new { type = "string", description = "Building name, floor, unit number. NEVER include 'Head Office', 'สำนักงานใหญ่', or 'Branch' here." },
                        address_line_3 = new { type = "string", description = "Additional address details. NEVER include 'Head Office', 'สำนักงานใหญ่', or 'Branch' here." },
                        district = new { type = "string", description = "Sub-district only (ตำบล/แขวง). e.g. 'คลองสวนพลู'" },

                        city = new { type = "string", description = "District only (อำเภอ/เขต). e.g. 'อยุธยา'" },
                        state_province = new { type = "string", description = "Province only (จังหวัด). e.g. 'อยุธยา'" },
                        postal_code = new { type = "string", description = "5-digit postal code, copied exactly from input" },
                        recipient_name = new { type = "string", description = "Shipping recipient full name (ชื่อผู้รับ). Only for Shipping addresses." },
                        recipient_phone = new { type = "string", description = "Shipping recipient phone number. Only for Shipping addresses." }
                    },
                    required = new[] { "address_line_1" }
                }
            }
        },
        required = new[] { "first_name", "last_name" }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractCustomerCommandHandler"/> class.
    /// </summary>
    /// <param name="instructionRepository">The system instruction repository.</param>
    /// <param name="geminiClient">The Gemini API client.</param>
    /// <param name="modelContextCacheService">The model context cache service.</param>
    /// <param name="logger">The logger.</param>
    public ExtractCustomerCommandHandler(
        ISystemInstructionRepository instructionRepository,
        IGeminiClient geminiClient,
        IModelContextCacheService modelContextCacheService,
        ILogger<ExtractCustomerCommandHandler> logger)
    {
        _instructionRepository = instructionRepository;
        _geminiClient = geminiClient;
        _modelContextCacheService = modelContextCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the extraction command.
    /// </summary>
    /// <param name="command">The extraction command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extraction result.</returns>
    public async Task<ExtractCustomerResult> HandleAsync(
        ExtractCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        // Load the customer-extraction prompt from the database
        var topicInstructions = await _instructionRepository.GetActiveByTopicsAsync(
            new[] { "customer-extraction" }, cancellationToken);

        var extractionInstruction = topicInstructions.FirstOrDefault();

        string systemPrompt;
        if (extractionInstruction != null)
        {
            systemPrompt = extractionInstruction.PersonaDefinition;
            if (!string.IsNullOrWhiteSpace(extractionInstruction.BusinessConstraints))
            {
                systemPrompt += "\n\n" + extractionInstruction.BusinessConstraints;
            }
        }
        else
        {
            _logger.LogWarning("Customer extraction prompt not found in database. Using fallback.");
            systemPrompt = "Extract customer information from the following data. Return all fields you can identify as JSON.";
        }

        // Build the user message with text content
        var userMessageParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(command.RawText))
        {
            userMessageParts.Add($"Text content:\n{command.RawText}");
        }

        // Build attachments from file data for multimodal extraction
        var attachments = new List<GeminiAttachment>();

        if (command.Files != null && command.Files.Count > 0)
        {
            foreach (var file in command.Files)
            {
                var contentType = file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    ? "image"
                    : file.MimeType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                        ? "PDF"
                        : "document";

                attachments.Add(new GeminiAttachment
                {
                    ContentType = contentType,
                    Data = file.Base64Data,
                    MimeType = file.MimeType
                });
            }

            userMessageParts.Add($"Extract customer information from the {attachments.Count} attached file(s).");
        }

        if (command.StoragePaths.Count > 0 && attachments.Count == 0)
        {
            userMessageParts.Add($"Storage paths (for reference): {string.Join(", ", command.StoragePaths)}");
        }

        if (userMessageParts.Count == 0 && attachments.Count == 0)
        {
            return new ExtractCustomerResult { Success = false, ErrorMessage = "No files or text provided." };
        }

        var userMessage = userMessageParts.Count > 0
            ? string.Join("\n\n", userMessageParts)
            : "Extract customer information from the attached files.";

        // Call Gemini directly with structured output
        var geminiRequest = new GeminiRequest
        {
            SystemInstruction = systemPrompt,
            Messages = new List<GeminiMessage>
            {
                new() { Role = "user", Content = userMessage }
            },
            Attachments = attachments.Count > 0 ? attachments : null,
            ResponseMimeType = "application/json",
            ResponseSchema = CustomerExtractionSchema,
            ThinkingBudget = 0,
            MaxTokens = MaxExtractionOutputTokens,
            MaxPromptTokens = MaxExtractionPromptTokens,
            MediaResolution = attachments.Count > 0 ? "MEDIA_RESOLUTION_MEDIUM" : null,
            ServiceTier = "flex",
            TimeoutSeconds = GeminiRequest.FlexInferenceTimeoutSeconds
        };

        var cacheReference = await _modelContextCacheService.GetOrCreateSystemInstructionCacheAsync(
            new ModelContextCacheRequest
            {
                SystemInstruction = systemPrompt
            },
            cancellationToken);
        if (cacheReference is not null)
        {
            geminiRequest.CachedContentName = cacheReference.CachedContentName;
            geminiRequest.SystemInstruction = string.Empty;
        }

        var geminiResponse = await _geminiClient.SendMessageAsync(geminiRequest, cancellationToken);

        // Log raw Gemini response for debugging
        _logger.LogInformation("Raw Gemini extraction response: {Response}", geminiResponse.Content);

        if (!geminiResponse.Success)
        {
            _logger.LogError("Gemini extraction call failed: {Error} (Type: {ErrorType})",
                geminiResponse.ErrorMessage, geminiResponse.ErrorType);

            return new ExtractCustomerResult
            {
                Success = false,
                ErrorMessage = geminiResponse.ErrorMessage ?? "AI extraction failed."
            };
        }

        // Parse the JSON response
        try
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            var extracted = JsonSerializer.Deserialize<ExtractedCustomerData>(geminiResponse.Content, options);

            if (extracted == null)
            {
                return new ExtractCustomerResult { Success = false, ErrorMessage = "Failed to parse extracted data." };
            }

            // Log extracted addresses to verify AI populated address_line_1
            if (extracted.Addresses != null && extracted.Addresses.Count > 0)
            {
                foreach (var addr in extracted.Addresses)
                {
                    _logger.LogInformation(
                        "Extracted address ({Type}): Line1='{Line1}', District='{District}', City='{City}', PostalCode='{PostalCode}'",
                        addr.Type, addr.AddressLine1, addr.District, addr.City, addr.PostalCode);
                }
            }
            else
            {
                _logger.LogWarning("No addresses extracted by Gemini");
            }

            return new ExtractCustomerResult { Success = true, Data = extracted };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize extraction response: {Content}", geminiResponse.Content);
            return new ExtractCustomerResult { Success = false, ErrorMessage = $"Failed to parse AI response: {ex.Message}" };
        }
    }
}

/// <summary>
/// Result of the customer extraction operation.
/// </summary>
public class ExtractCustomerResult
{
    /// <summary>Gets or sets whether the extraction was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the error message if unsuccessful.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the extracted customer data.</summary>
    public ExtractedCustomerData? Data { get; set; }
}

/// <summary>
/// Extracted customer data from AI.
/// </summary>
public class ExtractedCustomerData
{
    /// <summary>Gets or sets the extracted first name.</summary>
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the extracted last name.</summary>
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    /// <summary>Gets or sets the extracted email.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Gets or sets the extracted mobile phone.</summary>
    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    /// <summary>Gets or sets the extracted landline phone.</summary>
    [JsonPropertyName("landline")]
    public string? Landline { get; set; }

    /// <summary>Gets or sets the extracted extension.</summary>
    [JsonPropertyName("extension")]
    public string? Extension { get; set; }

    /// <summary>Gets or sets the extracted segment.</summary>
    [JsonPropertyName("segment")]
    public string? Segment { get; set; }

    /// <summary>Gets or sets the extracted company name.</summary>
    [JsonPropertyName("company_name")]
    public string? CompanyName { get; set; }

    /// <summary>Gets or sets the extracted company phone.</summary>
    [JsonPropertyName("company_phone")]
    public string? CompanyPhone { get; set; }

    /// <summary>Gets or sets the extracted VAT number.</summary>
    [JsonPropertyName("vat_number")]
    public string? VatNumber { get; set; }

    /// <summary>Gets or sets the extracted branch number (สาขาที่).</summary>
    [JsonPropertyName("branch_number")]
    public string? BranchNumber { get; set; }

    /// <summary>Gets or sets the extracted addresses.</summary>
    [JsonPropertyName("addresses")]
    public List<ExtractedAddressData>? Addresses { get; set; }
}

/// <summary>
/// An extracted address from AI.
/// </summary>
public class ExtractedAddressData
{
    /// <summary>Gets or sets the address type.</summary>
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

