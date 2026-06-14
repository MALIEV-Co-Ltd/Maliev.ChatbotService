using System.Net;
using System.Diagnostics.Metrics;
using System.Text;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiClientStreamingTests
{
    [Fact]
    public async Task StreamMessageAsync_SseResponse_EmitsDeltasAndFinalResponse()
    {
        var handler = new GeminiStreamingHandler([
            """
            data: {"candidates":[{"content":{"parts":[{"text":"Hello "}]},"finishReason":"STOP"}]}
            """,
            """
            data: {"candidates":[{"content":{"parts":[{"text":"world"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":2,"totalTokenCount":5}}
            """
        ]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:MainModelName"] = "gemini-test"
            })
            .Build();
        var client = new GeminiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            configuration,
            new ConversationMetrics(CreateMeterFactory(), configuration),
            NullLogger<GeminiClient>.Instance);

        var events = new List<GeminiStreamEvent>();
        await foreach (var streamEvent in client.StreamMessageAsync(new GeminiRequest
        {
            SystemInstruction = "You are a test assistant.",
            Messages = [new GeminiMessage { Role = "user", Content = "Say hello" }]
        }))
        {
            events.Add(streamEvent);
        }

        Assert.Equal("started", events[0].Type);
        Assert.Equal(
            ["Hello ", "world"],
            events.Where(item => item.Type == "delta").Select(item => item.Delta ?? string.Empty).ToArray());
        var final = Assert.Single(events, item => item.Type == "final");
        Assert.NotNull(final.Response);
        Assert.True(final.Response.Success);
        Assert.Equal("Hello world", final.Response.Content);
        Assert.NotNull(final.Response.TokenUsage);
        Assert.Equal(5, final.Response.TokenUsage.TotalTokens);
        Assert.EndsWith("v1beta/models/gemini-test:streamGenerateContent?alt=sse", handler.RequestUri?.ToString());
        Assert.Equal("test-key", handler.ApiKey);
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var factory = new Mock<IMeterFactory>();
        factory.Setup(item => item.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("test-chatbot-streaming"));
        return factory.Object;
    }

    private sealed class GeminiStreamingHandler(IReadOnlyList<string> sseLines) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.SingleOrDefault()
                : null;
            var body = string.Join("\n\n", sseLines) + "\n\n";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
            });
        }
    }
}
