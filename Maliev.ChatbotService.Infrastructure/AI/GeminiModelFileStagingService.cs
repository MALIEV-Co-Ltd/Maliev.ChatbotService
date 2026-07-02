using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Uploads model input files to the Gemini Files API for temporary fileData prompting.
/// </summary>
public sealed class GeminiModelFileStagingService : IModelFileStagingService
{
    private const int DefaultProcessingPollAttempts = 12;
    private const int DefaultProcessingPollDelayMilliseconds = 5000;
    private const int MaxProcessingPollAttempts = 60;
    private const int MaxProcessingPollDelayMilliseconds = 60000;
    private const string ProcessingPollAttemptsConfigurationKey = "Gemini:FileApi:ProcessingPollAttempts";
    private const string ProcessingPollDelayConfigurationKey = "Gemini:FileApi:ProcessingPollDelayMs";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiModelFileStagingService> _logger;
    private readonly string _apiKey;
    private readonly int _processingPollAttempts;
    private readonly TimeSpan _processingPollDelay;

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
        _apiKey = GeminiApiConfiguration.ResolveApiKey(configuration);
        _processingPollAttempts = Math.Clamp(
            configuration.GetValue<int?>(ProcessingPollAttemptsConfigurationKey) ?? DefaultProcessingPollAttempts,
            0,
            MaxProcessingPollAttempts);
        _processingPollDelay = TimeSpan.FromMilliseconds(Math.Clamp(
            configuration.GetValue<int?>(ProcessingPollDelayConfigurationKey) ?? DefaultProcessingPollDelayMilliseconds,
            0,
            MaxProcessingPollDelayMilliseconds));
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
        var uploadedFile = await UploadAndFinalizeAsync(uploadUrl, request, cancellationToken);
        var result = await WaitForFileReadyAsync(uploadedFile, cancellationToken);

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
                $"Gemini Files API delete failed for {fileName}: {ModelProviderErrorSanitizer.Summarize(response, responseContent)}");
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
                $"Gemini Files API start upload failed: {ModelProviderErrorSanitizer.Summarize(response, responseContent)}");
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

    private async Task<GeminiFileMetadata> UploadAndFinalizeAsync(
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
                $"Gemini Files API upload failed: {ModelProviderErrorSanitizer.Summarize(response, responseContent)}");
        }

        return ParseFileMetadata(responseContent, request.MimeType);
    }

    private async Task<ModelFileReference> WaitForFileReadyAsync(
        GeminiFileMetadata file,
        CancellationToken cancellationToken)
    {
        var currentFile = file;
        var state = NormalizeState(currentFile.State);

        if (state == "ACTIVE")
        {
            return currentFile.ToReference();
        }

        if (state == "FAILED")
        {
            throw BuildFileProcessingFailedException(currentFile);
        }

        if (string.IsNullOrWhiteSpace(currentFile.Name))
        {
            if (state is null)
            {
                return currentFile.ToReference();
            }

            throw new InvalidOperationException(
                $"Gemini Files API upload returned state {state} but did not include file.name for readiness polling.");
        }

        var fileName = currentFile.Name;
        for (var attempt = 0; attempt < _processingPollAttempts; attempt++)
        {
            if (_processingPollDelay > TimeSpan.Zero)
            {
                await Task.Delay(_processingPollDelay, cancellationToken);
            }

            currentFile = await GetFileMetadataAsync(fileName, currentFile.MimeType, cancellationToken);
            state = NormalizeState(currentFile.State);
            if (state == "ACTIVE")
            {
                return currentFile.ToReference();
            }

            if (state == "FAILED")
            {
                throw BuildFileProcessingFailedException(currentFile);
            }
        }

        throw new InvalidOperationException(
            $"Gemini Files API file {fileName} remained in state {state ?? "UNKNOWN"} after {_processingPollAttempts} poll attempt(s).");
    }

    private async Task<GeminiFileMetadata> GetFileMetadataAsync(
        string fileName,
        string fallbackMimeType,
        CancellationToken cancellationToken)
    {
        var resourceName = fileName.TrimStart('/');
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"v1beta/{resourceName}");
        getRequest.Headers.Add("x-goog-api-key", _apiKey);

        var response = await _httpClient.SendAsync(getRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini Files API get failed for {fileName}: {ModelProviderErrorSanitizer.Summarize(response, responseContent)}");
        }

        return ParseFileMetadata(responseContent, fallbackMimeType);
    }

    private static GeminiFileMetadata ParseFileMetadata(string responseContent, string fallbackMimeType)
    {
        using var document = JsonDocument.Parse(responseContent);
        var file = document.RootElement.TryGetProperty("file", out var wrappedFile)
            ? wrappedFile
            : document.RootElement;
        var fileUri = TryGetString(file, "uri");
        if (string.IsNullOrWhiteSpace(fileUri))
        {
            throw new InvalidOperationException("Gemini Files API response did not include file.uri.");
        }

        return new GeminiFileMetadata(
            TryGetString(file, "name"),
            fileUri,
            TryGetString(file, "mimeType") ?? TryGetString(file, "mime_type") ?? fallbackMimeType,
            TryGetString(file, "state"),
            TryGetFailureMessage(file));
    }

    private static InvalidOperationException BuildFileProcessingFailedException(GeminiFileMetadata file)
    {
        var detail = string.IsNullOrWhiteSpace(file.FailureMessage)
            ? string.Empty
            : $" {file.FailureMessage}";

        return new InvalidOperationException(
            $"Gemini Files API file {file.Name ?? file.FileUri} entered FAILED state.{detail}");
    }

    private static string? NormalizeState(string? state) =>
        string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();

    private static string? TryGetFailureMessage(JsonElement file)
    {
        if (!file.TryGetProperty("error", out var error))
        {
            return null;
        }

        if (error.ValueKind == JsonValueKind.Object && TryGetString(error, "message") is { } message)
        {
            return message;
        }

        return error.ToString();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private sealed record GeminiFileMetadata(
        string? Name,
        string FileUri,
        string MimeType,
        string? State,
        string? FailureMessage)
    {
        public ModelFileReference ToReference() =>
            new()
            {
                Name = Name,
                FileUri = FileUri,
                MimeType = MimeType
            };
    }
}
