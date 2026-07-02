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
    public async Task SendMessageAsync_PromptOnlyRequest_SerializesPromptAsUserContent()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            Prompt = "What is stainless steel 304?"
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var content = doc.RootElement.GetProperty("contents")[0];
        Assert.Equal("user", content.GetProperty("role").GetString());
        Assert.Equal("What is stainless steel 304?", content.GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_Gemini25FlashWithoutThinkingBudget_DisablesThinkingByDefault()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:MainModelName"] = "gemini-2.5-flash"
        });

        await client.SendMessageAsync(new GeminiRequest
        {
            Prompt = "Summarize this customer note.",
            MaxTokens = 256
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var thinkingConfig = doc.RootElement
            .GetProperty("generationConfig")
            .GetProperty("thinkingConfig");
        Assert.Equal(0, thinkingConfig.GetProperty("thinkingBudget").GetInt32());
        Assert.False(thinkingConfig.TryGetProperty("includeThoughts", out _));
    }

    [Fact]
    public async Task SendMessageAsync_Gemini25ProWithoutThinkingBudget_DoesNotDisableRequiredThinking()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:MainModelName"] = "gemini-2.5-pro"
        });

        await client.SendMessageAsync(new GeminiRequest
        {
            Prompt = "Analyze this customer note."
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(
            doc.RootElement.TryGetProperty("generationConfig", out var generationConfig) &&
            generationConfig.TryGetProperty("thinkingConfig", out _));
    }

    [Fact]
    public async Task SendMessageAsync_ImageUrl_SerializesAsGeminiFileDataPart()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            Prompt = "Analyze this technical drawing",
            ImageUrl = "https://example.com/drawing.jpg?signature=abc",
            MaxTokens = 1500
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var parts = doc.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray().ToArray();
        Assert.Equal("Analyze this technical drawing", parts[0].GetProperty("text").GetString());
        var fileData = parts[1].GetProperty("fileData");
        Assert.Equal("https://example.com/drawing.jpg?signature=abc", fileData.GetProperty("fileUri").GetString());
        Assert.Equal("image/jpeg", fileData.GetProperty("mimeType").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_PromptFeedbackBlock_ReturnsValidationFallback()
    {
        var handler = new CapturingHandler("""{"promptFeedback":{"blockReason":"SAFETY","safetyRatings":[]}}""");
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            Prompt = "unsafe prompt"
        });

        Assert.False(response.Success);
        Assert.True(response.IsFallback);
        Assert.Equal("ValidationFailure", response.ErrorType);
        Assert.NotEmpty(response.Content);
    }

    [Fact]
    public async Task SendMessageAsync_CachedContentName_SerializesAsTopLevelCachedContent()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            Prompt = "Summarize the customer context.",
            CachedContentName = "cachedContents/customer-context-123",
            MaxTokens = 256
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("cachedContents/customer-context-123", doc.RootElement.GetProperty("cachedContent").GetString());
        Assert.True(doc.RootElement.TryGetProperty("generationConfig", out var generationConfig));
        Assert.False(generationConfig.TryGetProperty("cachedContent", out _));
        Assert.Equal("sys", doc.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("Summarize the customer context.", doc.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_BlankSystemInstruction_OmitsSystemInstruction()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = string.Empty,
            Prompt = "Continue from the cached system prompt.",
            CachedContentName = "cachedContents/customer-context-123"
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("cachedContents/customer-context-123", doc.RootElement.GetProperty("cachedContent").GetString());
        Assert.False(doc.RootElement.TryGetProperty("systemInstruction", out _));
    }

    [Fact]
    public async Task SendMessageAsync_StoreFalse_SerializesAsTopLevelStoreField()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            Prompt = "Handle private customer data.",
            Store = false,
            MaxTokens = 128
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(doc.RootElement.GetProperty("store").GetBoolean());
        Assert.True(doc.RootElement.TryGetProperty("generationConfig", out var generationConfig));
        Assert.False(generationConfig.TryGetProperty("store", out _));
    }

    [Fact]
    public async Task SendMessageAsync_ConfiguredDefaultSafetySettings_SerializesAsTopLevelSafetySettings()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:SafetySettings:Enabled"] = "true",
            ["Gemini:SafetySettings:Threshold"] = "BLOCK_ONLY_HIGH",
            ["Gemini:SafetySettings:Categories:0"] = "HARM_CATEGORY_HARASSMENT",
            ["Gemini:SafetySettings:Categories:1"] = "HARM_CATEGORY_DANGEROUS_CONTENT"
        });

        await client.SendMessageAsync(new GeminiRequest
        {
            Prompt = "Help with a customer request.",
            MaxTokens = 128
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var settings = doc.RootElement.GetProperty("safetySettings").EnumerateArray().ToArray();
        Assert.Equal(2, settings.Length);
        Assert.Contains(settings, setting =>
            setting.GetProperty("category").GetString() == "HARM_CATEGORY_HARASSMENT" &&
            setting.GetProperty("threshold").GetString() == "BLOCK_ONLY_HIGH");
        Assert.Contains(settings, setting =>
            setting.GetProperty("category").GetString() == "HARM_CATEGORY_DANGEROUS_CONTENT" &&
            setting.GetProperty("threshold").GetString() == "BLOCK_ONLY_HIGH");
        Assert.False(doc.RootElement.GetProperty("generationConfig").TryGetProperty("safetySettings", out _));
    }

    [Fact]
    public async Task SendMessageAsync_RequestSafetySettings_OverrideConfiguredDefaults()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:SafetySettings:Enabled"] = "true",
            ["Gemini:SafetySettings:Threshold"] = "BLOCK_ONLY_HIGH",
            ["Gemini:SafetySettings:Categories:0"] = "HARM_CATEGORY_HARASSMENT"
        });

        await client.SendMessageAsync(new GeminiRequest
        {
            Prompt = "Help with a customer request.",
            SafetySettings =
            [
                new GeminiSafetySetting
                {
                    Category = "HARM_CATEGORY_DANGEROUS_CONTENT",
                    Threshold = "BLOCK_MEDIUM_AND_ABOVE"
                }
            ]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var setting = Assert.Single(doc.RootElement.GetProperty("safetySettings").EnumerateArray());
        Assert.Equal("HARM_CATEGORY_DANGEROUS_CONTENT", setting.GetProperty("category").GetString());
        Assert.Equal("BLOCK_MEDIUM_AND_ABOVE", setting.GetProperty("threshold").GetString());
    }

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
    public async Task SendMessageAsync_FunctionCallHistory_SerializesThoughtSignatureOnFunctionCallPart()
    {
        var handler = new CapturingHandler(
            """{"candidates":[{"content":{"parts":[{"text":"done"}]},"finishReason":"STOP"}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
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
                            Id = "call-1",
                            ThoughtSignature = "thought-signature-1",
                            Args = new Dictionary<string, object> { ["part"] = "bracket" }
                        }
                    ]
                },
                new GeminiMessage
                {
                    Role = "user",
                    FunctionResponses =
                    [
                        new GeminiFunctionResponse { Name = "quote_get_state", Id = "call-1", ResponseJson = "{\"ready\":true}" }
                    ]
                }
            ]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var functionCallPart = doc.RootElement.GetProperty("contents")[1].GetProperty("parts")[0];
        Assert.Equal("thought-signature-1", functionCallPart.GetProperty("thoughtSignature").GetString());
        Assert.False(functionCallPart.GetProperty("functionCall").TryGetProperty("thoughtSignature", out _));
    }

    [Fact]
    public async Task SendMessageAsync_FunctionCallResponse_CapturesThoughtSignature()
    {
        var handler = new CapturingHandler("""
            {
              "candidates":[{
                "content":{
                  "parts":[{
                    "functionCall":{
                      "name":"quote_get_state",
                      "id":"call-1",
                      "args":{"part":"bracket"}
                    },
                    "thoughtSignature":"thought-signature-1"
                  }]
                },
                "finishReason":"STOP"
              }]
            }
            """);
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            Messages = [new GeminiMessage { Role = "user", Content = "price this part" }]
        });

        var functionCall = Assert.Single(response.FunctionCalls);
        Assert.Equal("thought-signature-1", functionCall.ThoughtSignature);
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
    public async Task SendMessageAsync_ExternalHttpsAttachment_SerializesAsFileData()
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
        var fileData = parts[1].GetProperty("fileData");
        Assert.Equal("https://signed.example.test/sketch.png?token=abc", fileData.GetProperty("fileUri").GetString());
        Assert.Equal("image/png", fileData.GetProperty("mimeType").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_ExternalHttpsAudioAttachment_SerializesAsFileData()
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
                    Content = "Transcribe this voice note.",
                    Attachments =
                    [
                        new GeminiAttachment
                        {
                            MimeType = "audio/mpeg",
                            Data = "https://signed.example.test/customer-voice-note.mp3?token=abc"
                        }
                    ]
                }
            ]
        };

        await client.SendMessageAsync(request);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var parts = doc.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray().ToArray();
        Assert.Equal("Transcribe this voice note.", parts[0].GetProperty("text").GetString());
        var fileData = parts[1].GetProperty("fileData");
        Assert.Equal("https://signed.example.test/customer-voice-note.mp3?token=abc", fileData.GetProperty("fileUri").GetString());
        Assert.Equal("audio/mpeg", fileData.GetProperty("mimeType").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_UnsupportedExternalHttpsAttachmentMime_SerializesAsTextReference()
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
                    Content = "Review this photo.",
                    Attachments =
                    [
                        new GeminiAttachment
                        {
                            MimeType = "image/heic",
                            Data = "https://signed.example.test/photo.heic?token=abc"
                        }
                    ]
                }
            ]
        };

        await client.SendMessageAsync(request);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var parts = doc.RootElement.GetProperty("contents")[0].GetProperty("parts").EnumerateArray().ToArray();
        Assert.False(parts[1].TryGetProperty("fileData", out _));
        Assert.Contains("Attached file reference", parts[1].GetProperty("text").GetString(), StringComparison.Ordinal);
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

    [Fact]
    public async Task SendMessageAsync_GenerationOptions_SerializeUnderGenerationConfig()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            MaxTokens = 128,
            Temperature = 0.2,
            ThinkingBudget = 0,
            MediaResolution = "MEDIA_RESOLUTION_MEDIUM",
            Messages = [new GeminiMessage { Role = "user", Content = "hi" }]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var generationConfig = doc.RootElement.GetProperty("generationConfig");
        Assert.Equal(128, generationConfig.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal(0.2, generationConfig.GetProperty("temperature").GetDouble());
        Assert.Equal(0, generationConfig.GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
        Assert.Equal("MEDIA_RESOLUTION_MEDIUM", generationConfig.GetProperty("mediaResolution").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_ServiceTier_SerializesAsDocumentedRestRequestField()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            ServiceTier = "flex",
            TimeoutSeconds = GeminiRequest.FlexInferenceTimeoutSeconds,
            Messages = [new GeminiMessage { Role = "user", Content = "hi" }]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("flex", doc.RootElement.GetProperty("serviceTier").GetString());
        Assert.False(doc.RootElement.TryGetProperty("service_tier", out _));
        Assert.False(
            doc.RootElement.TryGetProperty("generationConfig", out var generationConfig) &&
            generationConfig.TryGetProperty("serviceTier", out _));

        Assert.NotNull(handler.RequestHeaders);
        Assert.True(handler.RequestHeaders!.TryGetValue("X-Server-Timeout", out var serverTimeout));
        Assert.Equal("600", Assert.Single(serverTimeout));
    }

    [Fact]
    public async Task SendMessageAsync_FlexTierWithoutExplicitTimeout_UsesFlexInferenceServerTimeout()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            ServiceTier = "flex",
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize this background record." }]
        });

        Assert.NotNull(handler.RequestHeaders);
        Assert.True(handler.RequestHeaders!.TryGetValue("X-Server-Timeout", out var serverTimeout));
        Assert.Equal("600", Assert.Single(serverTimeout));
    }

    [Fact]
    public async Task StreamMessageAsync_FlexTierWithoutExplicitTimeout_UsesFlexInferenceServerTimeout()
    {
        var handler = new CapturingHandler("""
            data: {"candidates":[{"content":{"parts":[{"text":"ok"}]}}],"usageMetadata":{"serviceTier":"flex"}}

            """);
        var client = CreateClient(handler);

        await foreach (var _ in client.StreamMessageAsync(new GeminiRequest
        {
            ServiceTier = "flex",
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize this background record." }]
        }))
        {
        }

        Assert.NotNull(handler.RequestHeaders);
        Assert.True(handler.RequestHeaders!.TryGetValue("X-Server-Timeout", out var serverTimeout));
        Assert.Equal("600", Assert.Single(serverTimeout));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SendMessageAsync_FlexTransientFailure_RetriesWithFlexTier(HttpStatusCode statusCode)
    {
        var handler = new SequencedHandler(
        [
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("""{"error":{"message":"Flex transient failure"}}""", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""", Encoding.UTF8, "application/json")
            }
        ]);
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:FlexRetryBaseDelayMs"] = "0"
        });

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            ServiceTier = "flex",
            TimeoutSeconds = GeminiRequest.FlexInferenceTimeoutSeconds,
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize this background record." }]
        });

        Assert.True(response.Success);
        Assert.Equal("ok", response.Content);
        Assert.Equal(2, handler.RequestBodies.Count);

        foreach (var body in handler.RequestBodies)
        {
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("flex", doc.RootElement.GetProperty("serviceTier").GetString());
            Assert.False(doc.RootElement.TryGetProperty("service_tier", out _));
        }
    }

    [Fact]
    public async Task SendMessageAsync_BuiltInSearchOnly_DoesNotSerializeFunctionCallingConfig()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            EnableWebSearch = true,
            Messages = [new GeminiMessage { Role = "user", Content = "What is ISO 9001?" }]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var tools = doc.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Single(tools);
        Assert.True(tools[0].TryGetProperty("google_search", out _));
        Assert.False(tools[0].TryGetProperty("googleSearch", out _));
        Assert.False(doc.RootElement.TryGetProperty("toolConfig", out _));
    }

    [Fact]
    public async Task SendMessageAsync_UrlContextOnly_DoesNotSerializeFunctionCallingConfig()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            EnableUrlContext = true,
            Messages = [new GeminiMessage { Role = "user", Content = "Summarize https://example.com/materials/asa.pdf" }]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var tools = doc.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Single(tools);
        Assert.True(tools[0].TryGetProperty("url_context", out _));
        Assert.False(tools[0].TryGetProperty("urlContext", out _));
        Assert.False(doc.RootElement.TryGetProperty("toolConfig", out _));
    }

    [Fact]
    public async Task SendMessageAsync_BuiltInSearchAndUrlContext_SerializesBothBuiltInTools()
    {
        var handler = new CapturingHandler("""{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        var client = CreateClient(handler);

        await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            EnableWebSearch = true,
            EnableUrlContext = true,
            Messages = [new GeminiMessage { Role = "user", Content = "Compare latest ISO notes with https://example.com/iso-notes" }]
        });

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var tools = doc.RootElement.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Equal(2, tools.Length);
        Assert.Contains(tools, tool => tool.TryGetProperty("google_search", out _));
        Assert.Contains(tools, tool => tool.TryGetProperty("url_context", out _));
        Assert.DoesNotContain(tools, tool => tool.TryGetProperty("googleSearch", out _));
        Assert.DoesNotContain(tools, tool => tool.TryGetProperty("urlContext", out _));
        Assert.False(doc.RootElement.TryGetProperty("toolConfig", out _));
    }

    [Fact]
    public async Task SendMessageAsync_MaxPromptTokensExceeded_CountsTokensAndSkipsGeneration()
    {
        var handler = new RoutingHandler(_ => """{"totalTokens":20001}""");
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            SystemInstruction = "sys",
            ServiceTier = "flex",
            MaxPromptTokens = 20000,
            Messages = [new GeminiMessage { Role = "user", Content = "large customer document" }]
        });

        Assert.False(response.Success);
        Assert.True(response.IsFallback);
        Assert.Equal("GeminiInputTokenLimit", response.ErrorType);
        Assert.Single(handler.RequestUris);
        Assert.Contains(":countTokens", handler.RequestUris[0], StringComparison.Ordinal);
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains(":generateContent", StringComparison.Ordinal));

        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        var generateContentRequest = body.RootElement.GetProperty("generateContentRequest");
        Assert.Equal("flex", generateContentRequest.GetProperty("serviceTier").GetString());
        Assert.False(generateContentRequest.TryGetProperty("service_tier", out _));
        Assert.Equal("sys", generateContentRequest.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("large customer document", generateContentRequest.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_UsageMetadata_MapsCostRelevantTokenBreakdown()
    {
        var handler = new CapturingHandler("""
            {
              "candidates":[{"content":{"parts":[{"text":"ok"}]}}],
              "usageMetadata":{
                "promptTokenCount":100,
                "cachedContentTokenCount":25,
                "toolUsePromptTokenCount":10,
                "thoughtsTokenCount":15,
                "candidatesTokenCount":20,
                "totalTokenCount":145,
                "promptTokensDetails":[
                  {"modality":"TEXT","tokenCount":70},
                  {"modality":"AUDIO","tokenCount":30}
                ],
                "cacheTokensDetails":[
                  {"modality":"TEXT","tokenCount":25}
                ],
                "candidatesTokensDetails":[
                  {"modality":"TEXT","tokenCount":20}
                ],
                "toolUsePromptTokensDetails":[
                  {"modality":"TEXT","tokenCount":10}
                ]
              }
            }
            """);
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "hi" }]
        });

        Assert.NotNull(response.TokenUsage);
        Assert.Equal(100, response.TokenUsage.PromptTokens);
        Assert.Equal(25, response.TokenUsage.CachedPromptTokens);
        Assert.Equal(10, response.TokenUsage.ToolUsePromptTokens);
        Assert.Equal(15, response.TokenUsage.ThoughtTokens);
        Assert.Equal(20, response.TokenUsage.CompletionTokens);
        Assert.Equal(145, response.TokenUsage.TotalTokens);
        Assert.Collection(
            response.TokenUsage.PromptTokenDetails,
            item =>
            {
                Assert.Equal("TEXT", item.Modality);
                Assert.Equal(70, item.TokenCount);
            },
            item =>
            {
                Assert.Equal("AUDIO", item.Modality);
                Assert.Equal(30, item.TokenCount);
            });
        var cachedDetail = Assert.Single(response.TokenUsage.CachedTokenDetails);
        Assert.Equal("TEXT", cachedDetail.Modality);
        Assert.Equal(25, cachedDetail.TokenCount);
        var candidateDetail = Assert.Single(response.TokenUsage.CandidateTokenDetails);
        Assert.Equal("TEXT", candidateDetail.Modality);
        Assert.Equal(20, candidateDetail.TokenCount);
        var toolDetail = Assert.Single(response.TokenUsage.ToolUsePromptTokenDetails);
        Assert.Equal("TEXT", toolDetail.Modality);
        Assert.Equal(10, toolDetail.TokenCount);
    }

    [Fact]
    public async Task SendMessageAsync_GroundingMetadata_MapsWebSearchQueries()
    {
        var handler = new CapturingHandler("""
            {
              "candidates":[{
                "content":{"parts":[{"text":"ok"}]},
                "groundingMetadata":{
                  "webSearchQueries":["latest ISO 9001 requirements","official ASTM D638 source"]
                }
              }]
            }
            """);
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            EnableWebSearch = true,
            Messages = [new GeminiMessage { Role = "user", Content = "Find the latest ISO 9001 source" }]
        });

        Assert.Equal(
            ["latest ISO 9001 requirements", "official ASTM D638 source"],
            response.GroundingWebSearchQueries);
    }

    [Fact]
    public async Task SendMessageAsync_ResponseServiceTierHeader_MapsToResponse()
    {
        var handler = new CapturingHandler(
            """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""",
            ("x-gemini-service-tier", "flex"));
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            ServiceTier = "flex",
            Messages = [new GeminiMessage { Role = "user", Content = "hi" }]
        });

        Assert.Equal("flex", response.ServiceTier);
    }

    [Fact]
    public async Task SendMessageAsync_UsageMetadataServiceTier_MapsToResponse()
    {
        var handler = new CapturingHandler("""
            {
              "candidates":[{"content":{"parts":[{"text":"ok"}]}}],
              "usageMetadata":{
                "serviceTier":"flex"
              }
            }
            """);
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "hi" }]
        });

        Assert.Equal("flex", response.ServiceTier);
    }

    [Fact]
    public async Task StreamMessageAsync_UsageMetadataServiceTier_MapsToFinalResponse()
    {
        var handler = new CapturingHandler("""
            data: {"candidates":[{"content":{"parts":[{"text":"ok"}]}}],"usageMetadata":{"serviceTier":"flex"}}

            """);
        var client = CreateClient(handler);

        GeminiResponse? finalResponse = null;
        await foreach (var streamEvent in client.StreamMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "hi" }]
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

    private static GeminiClient CreateClient(
        HttpMessageHandler handler,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key",
            ["Gemini:MainModelName"] = "gemini-test"
        };

        if (extraConfiguration is not null)
        {
            foreach (var item in extraConfiguration)
            {
                configurationValues[item.Key] = item.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
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

    private sealed class CapturingHandler(
        string responseJson,
        params (string Name, string Value)[] responseHeaders) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        public Dictionary<string, string[]>? RequestHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestHeaders = request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            foreach (var (name, value) in responseHeaders)
            {
                response.Headers.Add(name, value);
            }

            return response;
        }
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, string> responseFactory) : HttpMessageHandler
    {
        public List<string> RequestUris { get; } = new();

        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri?.ToString() ?? string.Empty);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(request), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SequencedHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private int _index;

        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            var response = responses[Math.Min(_index, responses.Count - 1)];
            _index++;
            return response;
        }
    }
}
