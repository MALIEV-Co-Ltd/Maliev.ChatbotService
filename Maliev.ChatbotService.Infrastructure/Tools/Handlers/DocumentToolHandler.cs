using System.Text.Json;
using Maliev.ChatbotService.Infrastructure.Tools.Handlers;

namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Tool handler for Document and NDA operations.
/// </summary>
public class DocumentToolHandler(IHttpClientFactory httpClientFactory) : BaseToolHandler(httpClientFactory)
{
    /// <inheritdoc/>
    protected override string ServiceName => "CustomerService"; // Default service

    /// <inheritdoc/>
    public override async Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "list_customer_ndas" => await ListCustomerNDAsAsync(args, cancellationToken),
            "get_document_content" => await GetDocumentContentAsync(args, cancellationToken),
            _ => """{"error": "Unknown document tool"}"""
        };
    }

    private async Task<string> ListCustomerNDAsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var customerId = GetStringArg(args, "customer_id");
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return """{"error": "customer_id is required"}""";
        }
        return await GetAsync($"/customer/v1/ndas/customer/{customerId}", cancellationToken);
    }

    private async Task<string> GetDocumentContentAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        string fileReference = GetStringArg(args, "file_reference");
        string documentId = GetStringArg(args, "document_id");

        if (string.IsNullOrWhiteSpace(fileReference) && string.IsNullOrWhiteSpace(documentId))
        {
            return """{"error": "Either file_reference or document_id is required"}""";
        }

        // If we only have document_id, resolve it to file_reference first
        if (string.IsNullOrWhiteSpace(fileReference))
        {
            var docResponse = await GetAsync($"/customer/v1/documents/{documentId}", cancellationToken);
            try
            {
                using var doc = JsonDocument.Parse(docResponse);
                if (doc.RootElement.TryGetProperty("fileReference", out var fileRefProp))
                {
                    fileReference = fileRefProp.GetString() ?? "";
                }
            }
            catch
            {
                return """{"error": "Failed to parse document response"}""";
            }

            if (string.IsNullOrWhiteSpace(fileReference))
            {
                return """{"error": "Could not resolve file_reference from document_id"}""";
            }
        }

        // Now we have fileReference, get signed URL from UploadService
        var uploadClient = _httpClientFactory.CreateClient("UploadService");
        var signedUrlResponse = await uploadClient.PostAsync($"/upload/v1/files/{fileReference}/signed-url", null, cancellationToken);
        
        if (!signedUrlResponse.IsSuccessStatusCode)
        {
             return $$"""{"error": "Failed to get signed URL: {{signedUrlResponse.StatusCode}}"}""";
        }

        var signedUrlJson = await signedUrlResponse.Content.ReadAsStringAsync(cancellationToken);
        string downloadUrl = "";
        try {
            using var doc = JsonDocument.Parse(signedUrlJson);
            if (doc.RootElement.TryGetProperty("url", out var urlProp))
            {
                downloadUrl = urlProp.GetString() ?? "";
            }
        } catch { }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
             return """{"error": "Failed to parse signed URL response"}""";
        }

        // Download the file content
        var httpClient = _httpClientFactory.CreateClient(); 
        var fileBytes = await httpClient.GetByteArrayAsync(downloadUrl, cancellationToken);

        // Return base64 so Gemini can parse it directly
        return JsonSerializer.Serialize(new
        {
            _metadata = new
            {
                is_file = true,
                mime_type = "application/pdf",
                data = Convert.ToBase64String(fileBytes)
            },
            status = "Success",
            message = "Document content has been loaded and attached to this response for you to read."
        });
    }
}
