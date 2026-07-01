using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Uploads model input files to the Gemini Files API for temporary fileData prompting.
/// </summary>
public sealed class GeminiModelFileStagingService : IModelFileStagingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiModelFileStagingService> _logger;
    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiModelFileStagingService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">The logger.</param>
    public GeminiModelFileStagingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiModelFileStagingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ??
            throw new InvalidOperationException("Gemini API key is not configured.");
    }

    /// <inheritdoc/>
    public async Task<ModelFileReference?> StageFileAsync(
        ModelFileStagingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Content.Length == 0)
        {
            return null;
        }

        var uploadUrl = await StartUploadAsync(request, cancellationToken);
        var result = await UploadAndFinalizeAsync(uploadUrl, request, cancellationToken);

        _logger.LogInformation(
            "Staged Gemini file {FileName} as {FileUri}",
            request.FileName,
            result.FileUri);

        return result;
    }

    /// <inheritdoc/>
    public async Task DeleteFileAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var resourceName = fileName.TrimStart('/');
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"v1beta/{resourceName}");
        deleteRequest.Headers.Add("x-goog-api-key", _apiKey);

        var response = await _httpClient.SendAsync(deleteRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini Files API delete failed for {fileName}: {(int)response.StatusCode} {responseContent}");
        }

        _logger.LogInformation("Deleted staged Gemini file {FileName}", fileName);
    }

    private async Task<Uri> StartUploadAsync(
        ModelFileStagingRequest request,
        CancellationToken cancellationToken)
    {
        using var startRequest = new HttpRequestMessage(HttpMethod.Post, "upload/v1beta/files");
        startRequest.Headers.Add("x-goog-api-key", _apiKey);
        startRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        startRequest.Headers.Add("X-Goog-Upload-Command", "start");
        startRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", request.Content.Length.ToString());
        startRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", request.MimeType);
        startRequest.Content = new StringContent(
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?>
                {
                    ["display_name"] = request.FileName
                }
            }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(startRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini Files API start upload failed: {(int)response.StatusCode} {responseContent}");
        }

        if (!response.Headers.TryGetValues("X-Goog-Upload-URL", out var values) &&
            !response.Headers.TryGetValues("x-goog-upload-url", out values))
        {
            throw new InvalidOperationException("Gemini Files API start upload did not return an upload URL.");
        }

        var uploadUrl = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(uploadUrl))
        {
            throw new InvalidOperationException("Gemini Files API start upload returned an empty upload URL.");
        }

        return Uri.TryCreate(uploadUrl, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(_httpClient.BaseAddress!, uploadUrl);
    }

    private async Task<ModelFileReference> UploadAndFinalizeAsync(
        Uri uploadUrl,
        ModelFileStagingRequest request,
        CancellationToken cancellationToken)
    {
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Headers.Add("x-goog-api-key", _apiKey);
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.Content = new ByteArrayContent(request.Content);
        uploadRequest.Content.Headers.ContentLength = request.Content.Length;
        uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(request.MimeType);

        var response = await _httpClient.SendAsync(uploadRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini Files API upload failed: {(int)response.StatusCode} {responseContent}");
        }

        using var document = JsonDocument.Parse(responseContent);
        var file = document.RootElement.GetProperty("file");
        var fileUri = file.GetProperty("uri").GetString();
        if (string.IsNullOrWhiteSpace(fileUri))
        {
            throw new InvalidOperationException("Gemini Files API upload response did not include file.uri.");
        }

        return new ModelFileReference
        {
            Name = file.TryGetProperty("name", out var name) ? name.GetString() : null,
            FileUri = fileUri,
            MimeType = TryGetString(file, "mimeType") ??
                TryGetString(file, "mime_type") ??
                request.MimeType
        };
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;
}
