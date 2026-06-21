using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Verifies that tool turns are serialized to the Gemini REST API as native functionCall /
/// functionResponse parts (C1), not as plain text.
/// </summary>
public sealed class GeminiClientFunctionCallSerializationTests
{
    [Fact]
    public async Task SendMessageAsync_ToolTurns_SerializeAsNativeFunctionCallAndFunctionResponseParts()
    {
        var handler = new CapturingHandler(
            """{"candidates":[{"content":{"parts":[{"text":"done"}]},"finishReason":"STOP"}]}""");
        var client = CreateClient(handler);

        var request = new GeminiRequest
        {
            SystemInstruction = "sys",
            Messages =
            [
                new GeminiMessage { Role = "user", Content = "price this part" },
                new GeminiMessage
                {
                    Role = "assistant",
                    FunctionCalls =
                    [
                        new GeminiFunctionCall
                        {
                            Name = "quote_get_state",
                            Args = new Dictionary<string, object> { ["part"] = "bracket" }
                        }
                    ]
                },
                new GeminiMessage
                {
                    Role = "user",
                    FunctionResponses =
                    [
                        new GeminiFunctionResponse { Name = "quote_get_state", ResponseJson = "{\"ready\":true}" }
                    ]
                }
            ]
        };

        var response = await client.SendMessageAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(handler.RequestBody);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var contents = doc.RootElement.GetProperty("contents");
        Assert.Equal(3, contents.GetArrayLength());

        // Tool-call turn -> role "model" with a functionCall part.
        var modelTurn = contents[1];
        Assert.Equal("model", modelTurn.GetProperty("role").GetString());
        var functionCall = modelTurn.GetProperty("parts")[0].GetProperty("functionCall");
        Assert.Equal("quote_get_state", functionCall.GetProperty("name").GetString());
        Assert.Equal("bracket", functionCall.GetProperty("args").GetProperty("part").GetString());

        // Tool-result turn -> role "user" with a functionResponse part whose response is an OBJECT.
        var responseTurn = contents[2];
        Assert.Equal("user", responseTurn.GetProperty("role").GetString());
        var functionResponse = responseTurn.GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.Equal("quote_get_state", functionResponse.GetProperty("name").GetString());
        var responseObj = functionResponse.GetProperty("response");
        Assert.Equal(JsonValueKind.Object, responseObj.ValueKind);
        Assert.True(responseObj.GetProperty("ready").GetBoolean());

        // The old degraded representations must be gone.
        Assert.DoesNotContain("[Function result", handler.RequestBody);
    }

    [Fact]
    public async Task SendMessageAsync_NonObjectToolResult_WrappedAsResultObject()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        var request = new GeminiRequest
        {
            Messages =
            [
                new GeminiMessage { Role = "user", Content = "hi" },
                new GeminiMessage
                {
                    Role = "user",
                    FunctionResponses =
                    [
                        new GeminiFunctionResponse { Name = "t", ResponseJson = "\"plain string\"" }
                    ]
                }
            ]
        };

        await client.SendMessageAsync(request);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var functionResponse = doc.RootElement.GetProperty("contents")[1]
            .GetProperty("parts")[0].GetProperty("functionResponse");
        var responseObj = functionResponse.GetProperty("response");
        Assert.Equal(JsonValueKind.Object, responseObj.ValueKind);
        Assert.Equal("plain string", responseObj.GetProperty("result").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_ExternalHttpsAttachment_SerializesAsTextReferenceNotFileData()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        var request = new GeminiRequest
        {
            Messages =
            [
                new GeminiMessage
                {
                    Role = "user",
                    Content = "Review this sketch.",
                    Attachments =
                    [
                        new GeminiAttachment
                        {
                            MimeType = "image/png",
                            Data = "https://signed.example.test/sketch.png?token=abc"
                        }
                    ]
                }
            ]
        };

        await client.SendMessageAsync(request);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var parts = doc.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray().ToArray();
        Assert.Equal("Review this sketch.", parts[0].GetProperty("text").GetString());
        Assert.True(parts[1].TryGetProperty("text", out var referenceText));
        Assert.Contains("https://signed.example.test/sketch.png?token=abc", referenceText.GetString(), StringComparison.Ordinal);
        Assert.False(parts[1].TryGetProperty("fileData", out _));
        Assert.DoesNotContain("fileData", handler.RequestBody);
    }

    [Fact]
    public async Task SendMessageAsync_GcsAttachment_SerializesAsGeminiFileData()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        var request = new GeminiRequest
        {
            Messages =
            [
                new GeminiMessage
                {
                    Role = "user",
                    Content = "Review this drawing.",
                    Attachments =
                    [
                        new GeminiAttachment
                        {
                            MimeType = "application/pdf",
                            Data = "gs://maliev-bucket/drawing.pdf"
                        }
                    ]
                }
            ]
        };

        await client.SendMessageAsync(request);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var fileData = doc.RootElement.GetProperty("contents")[0]
            .GetProperty("parts")[1]
            .GetProperty("fileData");
        Assert.Equal("gs://maliev-bucket/drawing.pdf", fileData.GetProperty("fileUri").GetString());
        Assert.Equal("application/pdf", fileData.GetProperty("mimeType").GetString());
    }

    private static GeminiClient CreateClient(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:MainModelName"] = "gemini-test"
            })
            .Build();

        return new GeminiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            configuration,
            new ConversationMetrics(CreateMeterFactory(), configuration),
            NullLogger<GeminiClient>.Instance);
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var factory = new Mock<IMeterFactory>();
        factory.Setup(item => item.Create(It.IsAny<MeterOptions>())).Returns(new Meter("test-fc-serialization"));
        return factory.Object;
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
