using System.Net;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiModelFileStagingServiceTests
{
    [Fact]
    public async Task StageFileAsync_UsesGeminiResumableUploadAndReturnsFileUri()
    {
        var handler = new CapturingHandler(
            new CapturedResponse(
                HttpStatusCode.OK,
                string.Empty,
                new Dictionary<string, string>
                {
                    ["X-Goog-Upload-URL"] = "https://generativelanguage.googleapis.com/upload-session"
                }),
            new CapturedResponse(
                HttpStatusCode.OK,
                """
                {
                  "file": {
                    "name": "files/customer-form",
                    "uri": "https://generativelanguage.googleapis.com/v1beta/files/customer-form",
                    "mimeType": "application/pdf"
                  }
                }
                """));
        var service = CreateService(handler);

        var result = await service.StageFileAsync(new ModelFileStagingRequest
        {
            FileName = "customer-form.pdf",
            MimeType = "application/pdf",
            Content = "pdf bytes"u8.ToArray()
        });

        Assert.NotNull(result);
        Assert.Equal("files/customer-form", result!.Name);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/files/customer-form", result.FileUri);
        Assert.Equal("application/pdf", result.MimeType);
        Assert.Equal(2, handler.Requests.Count);

        var startRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, startRequest.Method);
        Assert.Equal("/upload/v1beta/files", startRequest.Uri.AbsolutePath);
        Assert.Equal("test-api-key", startRequest.Headers["x-goog-api-key"]);
        Assert.Equal("resumable", startRequest.Headers["X-Goog-Upload-Protocol"]);
        Assert.Equal("start", startRequest.Headers["X-Goog-Upload-Command"]);
        Assert.Equal("9", startRequest.Headers["X-Goog-Upload-Header-Content-Length"]);
        Assert.Equal("application/pdf", startRequest.Headers["X-Goog-Upload-Header-Content-Type"]);
        using var startPayload = JsonDocument.Parse(startRequest.BodyText!);
        Assert.Equal(
            "customer-form.pdf",
            startPayload.RootElement.GetProperty("file").GetProperty("display_name").GetString());

        var uploadRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, uploadRequest.Method);
        Assert.Equal("https://generativelanguage.googleapis.com/upload-session", uploadRequest.Uri.ToString());
        Assert.Equal("0", uploadRequest.Headers["X-Goog-Upload-Offset"]);
        Assert.Equal("upload, finalize", uploadRequest.Headers["X-Goog-Upload-Command"]);
        Assert.Equal("pdf bytes"u8.ToArray(), uploadRequest.BodyBytes);
        Assert.Equal("application/pdf", uploadRequest.ContentHeaders["Content-Type"]);
    }

    [Fact]
    public async Task DeleteFileAsync_SendsGeminiFilesDeleteRequest()
    {
        var handler = new CapturingHandler(new CapturedResponse(HttpStatusCode.OK, "{}"));
        var service = CreateService(handler);

        await service.DeleteFileAsync("files/customer-form");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/v1beta/files/customer-form", request.Uri.AbsolutePath);
        Assert.Equal("test-api-key", request.Headers["x-goog-api-key"]);
    }

    private static GeminiModelFileStagingService CreateService(CapturingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-api-key"
            })
            .Build();

        return new GeminiModelFileStagingService(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            configuration,
            NullLogger<GeminiModelFileStagingService>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<CapturedResponse> _responses;

        public CapturingHandler(params CapturedResponse[] responses)
        {
            _responses = new Queue<CapturedResponse>(responses);
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var bodyBytes = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);

            Requests.Add(new CapturedRequest
            {
                Method = request.Method,
                Uri = request.RequestUri!,
                Headers = request.Headers.ToDictionary(
                    header => header.Key,
                    header => string.Join(", ", header.Value),
                    StringComparer.OrdinalIgnoreCase),
                ContentHeaders = request.Content?.Headers.ToDictionary(
                    header => header.Key,
                    header => string.Join(", ", header.Value),
                    StringComparer.OrdinalIgnoreCase) ?? [],
                BodyBytes = bodyBytes,
                BodyText = bodyBytes.Length == 0 ? null : Encoding.UTF8.GetString(bodyBytes)
            });

            var capturedResponse = _responses.Dequeue();
            var response = new HttpResponseMessage(capturedResponse.StatusCode)
            {
                Content = new StringContent(capturedResponse.Body, Encoding.UTF8, "application/json")
            };

            foreach (var header in capturedResponse.Headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        }
    }

    private sealed class CapturedRequest
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;

        public Uri Uri { get; set; } = null!;

        public Dictionary<string, string> Headers { get; set; } = [];

        public Dictionary<string, string> ContentHeaders { get; set; } = [];

        public byte[] BodyBytes { get; set; } = [];

        public string? BodyText { get; set; }
    }

    private sealed record CapturedResponse(
        HttpStatusCode StatusCode,
        string Body,
        Dictionary<string, string>? Headers = null)
    {
        public Dictionary<string, string> Headers { get; } = Headers ?? [];
    }
}
