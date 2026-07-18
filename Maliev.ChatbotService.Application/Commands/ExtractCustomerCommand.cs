namespace Maliev.ChatbotService.Application.Commands;

/// <summary>
/// Command to extract customer data from documents or text using AI.
/// </summary>
public class ExtractCustomerCommand
{
    /// <summary>
    /// Gets or sets the storage paths of uploaded files to extract data from.
    /// </summary>
    public List<string> StoragePaths { get; set; } = [];

    /// <summary>
    /// Gets or sets optional raw text content to extract data from.
    /// </summary>
    public string? RawText { get; set; }

    /// <summary>
    /// Gets or sets file data for multimodal extraction (base64-encoded files).
    /// </summary>
    public List<ExtractionFileData>? Files { get; set; }
}

/// <summary>
/// File data for multimodal extraction.
/// </summary>
public class ExtractionFileData
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base64-encoded file content.
    /// </summary>
    public string Base64Data { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the file.
    /// </summary>
    public string MimeType { get; set; } = string.Empty;
}
