using System.Net;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task StreamMessageAsync_UsageMetadataWithoutCandidates_UpdatesFinalTokenUsage()
    {
        var handler = new GeminiStreamingHandler([
            """
            data: {"candidates":[{"content":{"parts":[{"text":"ok"}]},"finishReason":"STOP"}]}
            """,
            """
            data: {"usageMetadata":{"promptTokenCount":9,"cachedContentTokenCount":4,"candidatesTokenCount":2,"totalTokenCount":11,"serviceTier":"flex"}}
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
            Messages = [new GeminiMessage { Role = "user", Content = "Say ok" }]
        }))
        {
            events.Add(streamEvent);
        }

        var final = Assert.Single(events, item => item.Type == "final");
        Assert.NotNull(final.Response);
        Assert.True(final.Response.Success);
        Assert.Equal("ok", final.Response.Content);
        Assert.Equal("flex", final.Response.ServiceTier);
        Assert.NotNull(final.Response.TokenUsage);
        Assert.Equal(9, final.Response.TokenUsage.PromptTokens);
        Assert.Equal(4, final.Response.TokenUsage.CachedPromptTokens);
        Assert.Equal(2, final.Response.TokenUsage.CompletionTokens);
        Assert.Equal(11, final.Response.TokenUsage.TotalTokens);
    }

    [Fact]
    public async Task StreamMessageAsync_GroundingMetadata_MapsWebSearchQueriesToFinalResponse()
    {
        var handler = new GeminiStreamingHandler([
            """
            data: {"candidates":[{"content":{"parts":[{"text":"ok"}]},"groundingMetadata":{"webSearchQueries":["latest ISO 9001 source","official ASTM D638 source"]},"finishReason":"STOP"}]}
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
            EnableWebSearch = true,
            Messages = [new GeminiMessage { Role = "user", Content = "Find the latest ISO sources." }]
        }))
        {
            events.Add(streamEvent);
        }

        var final = Assert.Single(events, item => item.Type == "final");
        Assert.NotNull(final.Response);
        Assert.Equal(
            ["latest ISO 9001 source", "official ASTM D638 source"],
            final.Response.GroundingWebSearchQueries);
        Assert.Equal(1, final.Response.GoogleSearchGroundingPromptCount);
    }

    [Fact]
    public async Task StreamMessageAsync_PromptFeedbackBlock_ReturnsValidationFallback()
    {
        var handler = new GeminiStreamingHandler([
            """
            data: {"promptFeedback":{"blockReason":"SAFETY","safetyRatings":[]}}
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
            Messages = [new GeminiMessage { Role = "user", Content = "unsafe prompt" }]
        }))
        {
            events.Add(streamEvent);
        }

        var final = Assert.Single(events, item => item.Type == "final");
        Assert.NotNull(final.Response);
        Assert.False(final.Response.Success);
        Assert.True(final.Response.IsFallback);
        Assert.Equal("ValidationFailure", final.Response.ErrorType);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task StreamMessageAsync_FlexTransientFailure_RetriesWithFlexTier(HttpStatusCode statusCode)
    {
        var handler = new SequencedStreamingHandler(
        [
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("""{"error":{"message":"Flex transient failure"}}""", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    data: {"candidates":[{"content":{"parts":[{"text":"ok"}]},"finishReason":"STOP"}],"usageMetadata":{"serviceTier":"flex"}}

                    """, Encoding.UTF8, "text/event-stream")
            }
        ]);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:MainModelName"] = "gemini-test",
                ["Gemini:FlexRetryBaseDelayMs"] = "0"
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
            ServiceTier = "flex",
            TimeoutSeconds = GeminiRequest.FlexInferenceTimeoutSeconds,
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize this background record." }]
        }))
        {
            events.Add(streamEvent);
        }

        var final = Assert.Single(events, item => item.Type == "final");
        Assert.NotNull(final.Response);
        Assert.True(final.Response.Success);
        Assert.Equal("ok", final.Response.Content);
        Assert.Equal("flex", final.Response.ServiceTier);
        Assert.Equal(2, handler.RequestBodies.Count);

        foreach (var body in handler.RequestBodies)
        {
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("flex", doc.RootElement.GetProperty("serviceTier").GetString());
            Assert.False(doc.RootElement.TryGetProperty("service_tier", out _));
        }

        Assert.All(
            handler.RequestHeaders,
            headers =>
            {
                Assert.True(headers.TryGetValue("X-Server-Timeout", out var serverTimeout));
                Assert.Equal("600", Assert.Single(serverTimeout));
            });
    }

    [Fact]
    public async Task StreamMessageAsync_FunctionCallWithArrayArg_PreservesArrayStructureForToolForwarding()
    {
        // Gemini returns an array-of-object argument (cad_commands) on a tool call.
        var handler = new GeminiStreamingHandler([
            """
            data: {"candidates":[{"content":{"parts":[{"functionCall":{"name":"quote_generate_3d_preview","args":{"description":"Rectangular part 30x50x100mm","cad_commands":[{"op":"box","id":"part","params":[30,50,100]}]}}}]},"finishReason":"STOP"}]}
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
            Messages = [new GeminiMessage { Role = "user", Content = "How much for a 30x50x100mm part?" }]
        }))
        {
            events.Add(streamEvent);
        }

        var final = Assert.Single(events, item => item.Type == "final");
        Assert.NotNull(final.Response);
        var call = Assert.Single(final.Response.FunctionCalls);
        Assert.Equal("quote_generate_3d_preview", call.Name);
        Assert.True(call.Args.ContainsKey("cad_commands"));

        // QuoteEngineToolHandler forwards these args to the BFF using Web + SnakeCaseLower.
        // The BFF requires cad_commands to arrive as a JSON array (ValueKind.Array); if the
        // parser flattens the array to a raw JSON string, the BFF rejects it with
        // "At least one CAD command is required." Assert the array survives serialization.
        var forwardingOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var forwardedJson = JsonSerializer.Serialize(call.Args, forwardingOptions);
        using var forwarded = JsonDocument.Parse(forwardedJson);
        Assert.Equal(
            JsonValueKind.Array,
            forwarded.RootElement.GetProperty("cad_commands").ValueKind);
    }

    [Fact]
    public async Task StreamMessageAsync_WithThoughts_PutsThinkingConfigUnderGenerationConfig()
    {
        var handler = new GeminiStreamingHandler([
            """
            data: {"candidates":[{"content":{"parts":[{"text":"ok"}]},"finishReason":"STOP"}]}
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

        await foreach (var _ in client.StreamMessageAsync(new GeminiRequest
        {
            SystemInstruction = "You are a test assistant.",
            IncludeThoughts = true,
            Messages = [new GeminiMessage { Role = "user", Content = "Say hello" }]
        }))
        {
        }

        Assert.NotNull(handler.RequestBody);
        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(doc.RootElement.TryGetProperty("thinkingConfig", out _));
        var generationConfig = doc.RootElement.GetProperty("generationConfig");
        Assert.True(generationConfig.GetProperty("thinkingConfig").GetProperty("includeThoughts").GetBoolean());
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
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.SingleOrDefault()
                : null;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var body = string.Join("\n\n", sseLines) + "\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
            };
        }
    }

    private sealed class SequencedStreamingHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int _index;

        public List<string> RequestBodies { get; } = new();

        public List<Dictionary<string, string[]>> RequestHeaders { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestHeaders.Add(request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase));
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            var response = responses[Math.Min(_index, responses.Count - 1)];
            _index++;
            return response;
        }
    }
}
