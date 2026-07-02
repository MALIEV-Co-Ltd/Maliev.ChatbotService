using System.Net;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class OpenAICompatibleModelProviderClientTests
{
    [Fact]
    public async Task SendMessageAsync_PromptOnlyRequest_SerializesPromptAsUserMessage()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "Stainless steel 304 is an austenitic stainless steel."
                  }
                }
              ]
            }
            """, "application/json");
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "You are a manufacturing assistant.",
            Prompt = "What is stainless steel 304?"
        });

        Assert.True(response.Success);
        using var payload = JsonDocument.Parse(handler.RequestBody);
        var messages = payload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("What is stainless steel 304?", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_WithImageAndTools_UsesOpenAiCompatiblePayloadAndParsesToolCalls()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "I need the current quote state.",
                    "tool_calls": [
                      {
                        "type": "function",
                        "function": {
                          "name": "quote_engine_get_state",
                          "arguments": "{\"sessionId\":\"abc\",\"includeArtifacts\":true}"
                        }
                      }
                    ]
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 5,
                "total_tokens": 15
              }
            }
            """, "application/json");
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "You are a quote agent.",
            Messages =
            [
                new GeminiMessage
                {
                    Role = "user",
                    Content = "Check this bracket sketch.",
                    Attachments =
                    [
                        new GeminiAttachment
                        {
                            MimeType = "image/png",
                            Data = "aW1hZ2U="
                        }
                    ]
                }
            ],
            Tools =
            [
                new GeminiToolDeclaration
                {
                    FunctionDeclarations =
                    [
                        new GeminiFunctionDeclaration
                        {
                            Name = "quote_engine_get_state",
                            Description = "Gets quote state.",
                            Parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    sessionId = new { type = "string" },
                                    includeArtifacts = new { type = "boolean" }
                                }
                            }
                        }
                    ]
                }
            ]
        });

        Assert.True(response.Success);
        Assert.Equal("I need the current quote state.", response.Content);
        var functionCall = Assert.Single(response.FunctionCalls);
        Assert.Equal("quote_engine_get_state", functionCall.Name);
        Assert.Equal("abc", functionCall.Args["sessionId"]);
        Assert.Equal(true, functionCall.Args["includeArtifacts"]);
        Assert.NotNull(response.TokenUsage);
        Assert.Equal(15, response.TokenUsage.TotalTokens);
        Assert.Equal("Bearer test-key", handler.Authorization);

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("qwen-vl-test", payload.RootElement.GetProperty("model").GetString());
        Assert.False(payload.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("auto", payload.RootElement.GetProperty("tool_choice").GetString());
        var tools = payload.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Single(tools);
        Assert.Equal("function", tools[0].GetProperty("type").GetString());

        var messages = payload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        var contentParts = messages[1].GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("text", contentParts[0].GetProperty("type").GetString());
        Assert.Equal("image_url", contentParts[1].GetProperty("type").GetString());
        Assert.Equal(
            "data:image/png;base64,aW1hZ2U=",
            contentParts[1].GetProperty("image_url").GetProperty("url").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_WithAudioAttachment_SerializesInputAudioPart()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "transcribed"
                  }
                }
              ]
            }
            """, "application/json");
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Messages =
            [
                new GeminiMessage
                {
                    Role = "user",
                    Content = "Transcribe this audio.",
                    Attachments =
                    [
                        new GeminiAttachment
                        {
                            MimeType = "audio/wav",
                            Data = "data:audio/wav;base64,UklGRg=="
                        }
                    ]
                }
            ]
        });

        Assert.True(response.Success);
        using var payload = JsonDocument.Parse(handler.RequestBody);
        var messages = payload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        var contentParts = messages[0].GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("text", contentParts[0].GetProperty("type").GetString());
        Assert.Equal("input_audio", contentParts[1].GetProperty("type").GetString());

        var inputAudio = contentParts[1].GetProperty("input_audio");
        Assert.Equal("UklGRg==", inputAudio.GetProperty("data").GetString());
        Assert.Equal("wav", inputAudio.GetProperty("format").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_GeminiCostControls_SerializesServiceTierAndExtraBody()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "ok"
                  }
                }
              ]
            }
            """, "application/json");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize cached customer context." }],
            ServiceTier = "flex",
            CachedContentName = "cachedContents/customer-context-123",
            ThinkingBudget = 0,
            IncludeThoughts = true
        });

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("flex", payload.RootElement.GetProperty("service_tier").GetString());
        Assert.False(payload.RootElement.TryGetProperty("serviceTier", out _));

        var google = payload.RootElement.GetProperty("extra_body").GetProperty("google");
        Assert.Equal("cachedContents/customer-context-123", google.GetProperty("cached_content").GetString());

        var thinkingConfig = google.GetProperty("thinking_config");
        Assert.Equal(0, thinkingConfig.GetProperty("thinking_budget").GetInt32());
        Assert.True(thinkingConfig.GetProperty("include_thoughts").GetBoolean());
    }

    [Fact]
    public async Task SendMessageAsync_Gemini25FlashWithoutThinkingBudget_DisablesThinkingByDefault()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "ok"
                  }
                }
              ]
            }
            """, "application/json");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            ModelName = "gemini-2.5-flash",
            Messages = [new GeminiMessage { Role = "user", Content = "Classify this support message." }]
        });

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("gemini-2.5-flash", payload.RootElement.GetProperty("model").GetString());

        var google = payload.RootElement.GetProperty("extra_body").GetProperty("google");
        var thinkingConfig = google.GetProperty("thinking_config");
        Assert.Equal(0, thinkingConfig.GetProperty("thinking_budget").GetInt32());
        Assert.False(thinkingConfig.TryGetProperty("include_thoughts", out _));
    }

    [Fact]
    public async Task SendMessageAsync_Gemini25ProWithoutThinkingBudget_DoesNotDisableRequiredThinking()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "ok"
                  }
                }
              ]
            }
            """, "application/json");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            ModelName = "gemini-2.5-pro",
            Messages = [new GeminiMessage { Role = "user", Content = "Analyze this complex issue." }]
        });

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("gemini-2.5-pro", payload.RootElement.GetProperty("model").GetString());
        Assert.False(payload.RootElement.TryGetProperty("extra_body", out _));
    }

    [Fact]
    public async Task SendMessageAsync_StoreFalse_SerializesStoreFlag()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "ok"
                  }
                }
              ]
            }
            """, "application/json");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize private customer context." }],
            Store = false
        });

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.False(payload.RootElement.GetProperty("store").GetBoolean());
    }

    [Fact]
    public async Task SendMessageAsync_ServiceTierHeader_MapsActualTierToResponse()
    {
        var handler = new CapturingHandler("""
            {
              "choices": [
                {
                  "message": {
                    "content": "ok"
                  }
                }
              ]
            }
            """, "application/json", ("x-gemini-service-tier", "standard"));
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Triage this urgent issue." }],
            ServiceTier = "priority"
        });

        Assert.True(response.Success);
        Assert.Equal("standard", response.ServiceTier);
    }

    [Fact]
    public async Task SendMessageAsync_UsageDetails_MapsCachedAudioReasoningAndBodyServiceTier()
    {
        var handler = new CapturingHandler("""
            {
              "service_tier": "standard",
              "choices": [
                {
                  "message": {
                    "content": "ok"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 20,
                "total_tokens": 120,
                "prompt_tokens_details": {
                  "cached_tokens": 60,
                  "audio_tokens": 25
                },
                "completion_tokens_details": {
                  "reasoning_tokens": 7
                }
              }
            }
            """, "application/json");
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Transcribe and summarize this." }],
            ServiceTier = "priority"
        });

        Assert.True(response.Success);
        Assert.Equal("standard", response.ServiceTier);
        Assert.NotNull(response.TokenUsage);
        Assert.Equal(100, response.TokenUsage.PromptTokens);
        Assert.Equal(13, response.TokenUsage.CompletionTokens);
        Assert.Equal(7, response.TokenUsage.ThoughtTokens);
        Assert.Equal(60, response.TokenUsage.CachedPromptTokens);
        Assert.Equal(120, response.TokenUsage.TotalTokens);
        Assert.Contains(
            response.TokenUsage.PromptTokenDetails,
            item => item.Modality == "AUDIO" && item.TokenCount == 25);
        Assert.Contains(
            response.TokenUsage.PromptTokenDetails,
            item => item.Modality == "TEXT" && item.TokenCount == 75);
    }

    [Fact]
    public async Task StreamMessageAsync_WithoutTools_EmitsOpenAiCompatibleDeltasAndFinalResponse()
    {
        var handler = new CapturingHandler(string.Join("\n\n",
            """
            data: {"choices":[{"delta":{"content":"Hello "}}]}
            """,
            """
            data: {"choices":[{"delta":{"content":"world"}}]}
            """,
            "data: [DONE]"), "text/event-stream");
        var client = CreateClient(handler);

        var events = new List<GeminiStreamEvent>();
        await foreach (var streamEvent in client.StreamMessageAsync(new GeminiRequest
        {
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

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task StreamMessageAsync_UsageChunk_RequestsAndMapsTokenUsage()
    {
        var handler = new CapturingHandler(string.Join("\n\n",
            """
            data: {"choices":[{"delta":{"content":"ok"}}]}
            """,
            """
            data: {"choices":[{"delta":{}}],"usage":{"prompt_tokens":12,"completion_tokens":3,"total_tokens":15}}
            """,
            "data: [DONE]"), "text/event-stream");
        var client = CreateClient(handler);

        GeminiResponse? finalResponse = null;
        await foreach (var streamEvent in client.StreamMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize this." }]
        }))
        {
            if (streamEvent.Type.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                finalResponse = streamEvent.Response;
            }
        }

        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.True(payload.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean());

        Assert.NotNull(finalResponse);
        Assert.NotNull(finalResponse!.TokenUsage);
        Assert.Equal(12, finalResponse.TokenUsage.PromptTokens);
        Assert.Equal(3, finalResponse.TokenUsage.CompletionTokens);
        Assert.Equal(15, finalResponse.TokenUsage.TotalTokens);
    }

    [Fact]
    public async Task StreamMessageAsync_UsageChunk_MapsUsageDetailsAndBodyServiceTier()
    {
        var handler = new CapturingHandler(string.Join("\n\n",
            """
            data: {"choices":[{"delta":{"content":"ok"}}]}
            """,
            """
            data: {"service_tier":"standard","choices":[],"usage":{"prompt_tokens":100,"completion_tokens":20,"total_tokens":120,"prompt_tokens_details":{"cached_tokens":60,"audio_tokens":25},"completion_tokens_details":{"reasoning_tokens":7}}}
            """,
            "data: [DONE]"), "text/event-stream");
        var client = CreateClient(handler);

        GeminiResponse? finalResponse = null;
        await foreach (var streamEvent in client.StreamMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Transcribe and summarize this." }],
            ServiceTier = "priority"
        }))
        {
            if (streamEvent.Type.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                finalResponse = streamEvent.Response;
            }
        }

        Assert.NotNull(finalResponse);
        Assert.Equal("standard", finalResponse!.ServiceTier);
        Assert.NotNull(finalResponse.TokenUsage);
        Assert.Equal(100, finalResponse.TokenUsage.PromptTokens);
        Assert.Equal(13, finalResponse.TokenUsage.CompletionTokens);
        Assert.Equal(7, finalResponse.TokenUsage.ThoughtTokens);
        Assert.Equal(60, finalResponse.TokenUsage.CachedPromptTokens);
        Assert.Equal(120, finalResponse.TokenUsage.TotalTokens);
        Assert.Contains(
            finalResponse.TokenUsage.PromptTokenDetails,
            item => item.Modality == "AUDIO" && item.TokenCount == 25);
        Assert.Contains(
            finalResponse.TokenUsage.PromptTokenDetails,
            item => item.Modality == "TEXT" && item.TokenCount == 75);
    }

    [Fact]
    public async Task StreamMessageAsync_ServiceTierHeader_MapsActualTierToFinalResponse()
    {
        var handler = new CapturingHandler(string.Join("\n\n",
            """
            data: {"choices":[{"delta":{"content":"ok"}}]}
            """,
            "data: [DONE]"), "text/event-stream", ("x-gemini-service-tier", "flex"));
        var client = CreateClient(handler);

        GeminiResponse? finalResponse = null;
        await foreach (var streamEvent in client.StreamMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize this background record." }],
            ServiceTier = "flex"
        }))
        {
            if (streamEvent.Type.Equals("final", StringComparison.OrdinalIgnoreCase))
            {
                finalResponse = streamEvent.Response;
            }
        }

        Assert.NotNull(finalResponse);
        Assert.Equal("flex", finalResponse!.ServiceTier);
    }

    private static OpenAICompatibleModelProviderClient CreateClient(CapturingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:OpenAICompatible:ApiKey"] = "test-key",
                ["Llm:OpenAICompatible:ModelName"] = "qwen-vl-test"
            })
            .Build();

        return new OpenAICompatibleModelProviderClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            configuration,
            NullLogger<OpenAICompatibleModelProviderClient>.Instance);
    }

    private sealed class CapturingHandler(
        string responseBody,
        string mediaType,
        params (string Name, string Value)[] responseHeaders) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
            };

            foreach (var (name, value) in responseHeaders)
            {
                response.Headers.Add(name, value);
            }

            return response;
        }
    }
}
