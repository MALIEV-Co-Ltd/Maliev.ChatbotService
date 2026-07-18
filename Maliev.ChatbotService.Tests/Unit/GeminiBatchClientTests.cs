using System.Net;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiBatchClientTests
{
    [Fact]
    public async Task CreateInlineGenerateContentBatchAsync_SerializesInlineRequestsAndParsesOperation()
    {
        var handler = new CapturingHandler("""{"name":"batches/batch-123","metadata":{"state":"JOB_STATE_PENDING"}}""");
        var client = CreateClient(handler);

        var result = await client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
        {
            DisplayName = "expired-session-summaries",
            ModelName = "gemini-2.5-flash-lite",
            Priority = -10,
            Requests =
            [
                new ModelBatchGenerateContentRequest
                {
                    Request = new GeminiRequest
                    {
                        SystemInstruction = "Summarize the conversation.",
                        Messages =
                        [
                            new GeminiMessage { Role = "user", Content = "User: hello\nAssistant: hi" }
                        ],
                        ResponseMimeType = "application/json",
                        ResponseSchema = new
                        {
                            type = "object",
                            properties = new { topics = new { type = "array", items = new { type = "string" } } }
                        },
                        ThinkingBudget = 0,
                        MaxTokens = 1024
                    },
                    Metadata = new Dictionary<string, object?>
                    {
                        ["sessionId"] = "session-1",
                        ["userProfileId"] = "user-1"
                    }
                }
            ]
        });

        Assert.Equal("batches/batch-123", result.Name);
        Assert.Equal("JOB_STATE_PENDING", result.State);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/v1beta/models/gemini-2.5-flash-lite:batchGenerateContent", handler.Request.RequestUri!.AbsolutePath);
        Assert.True(handler.Request.Headers.Contains("x-goog-api-key"));

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var batch = payload.RootElement.GetProperty("batch");
        Assert.Equal("expired-session-summaries", batch.GetProperty("displayName").GetString());
        Assert.Equal("-10", batch.GetProperty("priority").GetString());
        Assert.False(batch.TryGetProperty("display_name", out _));
        Assert.False(batch.TryGetProperty("input_config", out _));

        var inlineRequest = batch
            .GetProperty("inputConfig")
            .GetProperty("requests")
            .GetProperty("requests")[0];

        Assert.Equal("session-1", inlineRequest.GetProperty("metadata").GetProperty("sessionId").GetString());
        var generateRequest = inlineRequest.GetProperty("request");
        Assert.Equal(
            "Summarize the conversation.",
            generateRequest.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(
            "User: hello\nAssistant: hi",
            generateRequest.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(
            "application/json",
            generateRequest.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        Assert.Equal(
            0,
            generateRequest.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
        Assert.False(generateRequest.TryGetProperty("serviceTier", out _));
    }

    [Fact]
    public async Task CreateInlineGenerateContentBatchAsync_ConfiguredDefaultSafetySettings_SerializesInlineRequestSettings()
    {
        var handler = new CapturingHandler("""{"name":"batches/batch-123","metadata":{"state":"JOB_STATE_PENDING"}}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:SafetySettings:Enabled"] = "true",
            ["Gemini:SafetySettings:Threshold"] = "BLOCK_ONLY_HIGH",
            ["Gemini:SafetySettings:Categories:0"] = "HARM_CATEGORY_HARASSMENT",
            ["Gemini:SafetySettings:Categories:1"] = "HARM_CATEGORY_DANGEROUS_CONTENT"
        });

        await client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
        {
            DisplayName = "expired-session-summaries",
            ModelName = "gemini-2.5-flash-lite",
            Priority = -10,
            Requests =
            [
                new ModelBatchGenerateContentRequest
                {
                    Request = new GeminiRequest
                    {
                        SystemInstruction = "Summarize the conversation.",
                        Messages =
                        [
                            new GeminiMessage { Role = "user", Content = "User: hello\nAssistant: hi" }
                        ],
                        ThinkingBudget = 0,
                        MaxTokens = 1024
                    }
                }
            ]
        });

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var generateRequest = payload.RootElement
            .GetProperty("batch")
            .GetProperty("inputConfig")
            .GetProperty("requests")
            .GetProperty("requests")[0]
            .GetProperty("request");
        var settings = generateRequest.GetProperty("safetySettings").EnumerateArray().ToArray();
        Assert.Equal(2, settings.Length);
        Assert.Contains(settings, setting =>
            setting.GetProperty("category").GetString() == "HARM_CATEGORY_HARASSMENT" &&
            setting.GetProperty("threshold").GetString() == "BLOCK_ONLY_HIGH");
        Assert.Contains(settings, setting =>
            setting.GetProperty("category").GetString() == "HARM_CATEGORY_DANGEROUS_CONTENT" &&
            setting.GetProperty("threshold").GetString() == "BLOCK_ONLY_HIGH");
    }

    [Fact]
    public async Task CreateInlineGenerateContentBatchAsync_Gemini25FlashWithoutThinkingBudget_DisablesThinkingByDefault()
    {
        var handler = new CapturingHandler("""{"name":"batches/batch-123","metadata":{"state":"JOB_STATE_PENDING"}}""");
        var client = CreateClient(handler);

        await client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
        {
            DisplayName = "expired-session-summaries",
            ModelName = "gemini-2.5-flash-lite",
            Requests =
            [
                new ModelBatchGenerateContentRequest
                {
                    Request = new GeminiRequest
                    {
                        SystemInstruction = "Summarize the conversation.",
                        Messages =
                        [
                            new GeminiMessage { Role = "user", Content = "User: hello\nAssistant: hi" }
                        ],
                        MaxTokens = 1024
                    }
                }
            ]
        });

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var thinkingConfig = payload.RootElement
            .GetProperty("batch")
            .GetProperty("inputConfig")
            .GetProperty("requests")
            .GetProperty("requests")[0]
            .GetProperty("request")
            .GetProperty("generationConfig")
            .GetProperty("thinkingConfig");
        Assert.Equal(0, thinkingConfig.GetProperty("thinkingBudget").GetInt32());
    }

    [Fact]
    public async Task CreateInlineGenerateContentBatchAsync_OpenAiCompatibleGeminiConfiguration_UsesCompatibleKeyAndModel()
    {
        var handler = new CapturingHandler("""{"name":"batches/batch-123","metadata":{"state":"JOB_STATE_PENDING"}}""");
        var client = CreateClient(handler, new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = null,
            ["Gemini:MainModelName"] = null,
            ["Llm:OpenAICompatible:ApiKey"] = "compatible-key",
            ["Llm:OpenAICompatible:ModelName"] = "gemini-2.5-flash-lite"
        });

        await client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
        {
            DisplayName = "expired-session-summaries",
            ModelName = null,
            Requests =
            [
                new ModelBatchGenerateContentRequest
                {
                    Request = new GeminiRequest
                    {
                        SystemInstruction = "Summarize the conversation.",
                        Messages =
                        [
                            new GeminiMessage { Role = "user", Content = "User: hello\nAssistant: hi" }
                        ],
                        MaxTokens = 1024
                    }
                }
            ]
        });

        Assert.Equal("/v1beta/models/gemini-2.5-flash-lite:batchGenerateContent", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("compatible-key", handler.Request.Headers.GetValues("x-goog-api-key").Single());
    }

    [Fact]
    public async Task CreateInlineGenerateContentBatchAsync_MaxPromptTokensExceeded_CountsTokensAndSkipsBatchSubmission()
    {
        var handler = new SequencedCapturingHandler(
            new CapturedResponse(HttpStatusCode.OK, """{"totalTokens":101}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
            {
                DisplayName = "expired-session-summaries",
                ModelName = "gemini-2.5-flash-lite",
                Requests =
                [
                    new ModelBatchGenerateContentRequest
                    {
                        Request = new GeminiRequest
                        {
                            SystemInstruction = "Summarize the conversation.",
                            Messages =
                            [
                                new GeminiMessage { Role = "user", Content = "User: very long conversation" }
                            ],
                            MaxTokens = 1024,
                            MaxPromptTokens = 100
                        }
                    }
                ]
            }));

        Assert.Equal("Gemini Batch API prompt token limit exceeded.", exception.Message);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1beta/models/gemini-2.5-flash-lite:countTokens", request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain(handler.Requests, item =>
            item.RequestUri!.AbsolutePath.Contains("batchGenerateContent", StringComparison.Ordinal));

        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal(
            "models/gemini-2.5-flash-lite",
            body.RootElement.GetProperty("generateContentRequest").GetProperty("model").GetString());
    }

    [Fact]
    public async Task CreateInlineGenerateContentBatchAsync_MaxPromptTokensWithinLimit_CountsTokensBeforeBatchSubmission()
    {
        var handler = new SequencedCapturingHandler(
            new CapturedResponse(HttpStatusCode.OK, """{"totalTokens":99}"""),
            new CapturedResponse(HttpStatusCode.OK, """{"name":"batches/batch-123","metadata":{"state":"JOB_STATE_PENDING"}}"""));
        var client = CreateClient(handler);

        var result = await client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
        {
            DisplayName = "expired-session-summaries",
            ModelName = "gemini-2.5-flash-lite",
            Requests =
            [
                new ModelBatchGenerateContentRequest
                {
                    Request = new GeminiRequest
                    {
                        SystemInstruction = "Summarize the conversation.",
                        Messages =
                        [
                            new GeminiMessage { Role = "user", Content = "User: short conversation" }
                        ],
                        MaxTokens = 1024,
                        MaxPromptTokens = 100
                    }
                }
            ]
        });

        Assert.Equal("batches/batch-123", result.Name);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/v1beta/models/gemini-2.5-flash-lite:countTokens", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/v1beta/models/gemini-2.5-flash-lite:batchGenerateContent", handler.Requests[1].RequestUri!.AbsolutePath);

        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal(
            "models/gemini-2.5-flash-lite",
            body.RootElement.GetProperty("generateContentRequest").GetProperty("model").GetString());
    }

    [Fact]
    public async Task GetBatchAsync_ParsesOperationStateAndInlineResponses()
    {
        var handler = new CapturingHandler("""
            {
              "name":"batches/batch-123",
              "done":true,
              "metadata":{"state":"JOB_STATE_SUCCEEDED"},
              "response":{
                "output":{
                  "inlinedResponses":{
                    "inlinedResponses":[{
                      "metadata":{"sessionId":"session-1"},
                      "response":{
                        "candidates":[{"content":{"parts":[{"text":"{\"topics\":[]}"}]}}],
                        "usageMetadata":{
                          "promptTokenCount":20,
                          "cachedContentTokenCount":5,
                          "candidatesTokenCount":5,
                          "totalTokenCount":25,
                          "serviceTier":"flex",
                          "promptTokensDetails":[{"modality":"TEXT","tokenCount":20}],
                          "cacheTokensDetails":[{"modality":"TEXT","tokenCount":5}],
                          "candidatesTokensDetails":[{"modality":"TEXT","tokenCount":5}]
                        }
                      }
                    }]
                  }
                }
              }
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetBatchAsync("batches/batch-123");

        Assert.Equal("batches/batch-123", result.Name);
        Assert.True(result.Done);
        Assert.Equal("JOB_STATE_SUCCEEDED", result.State);
        var response = Assert.Single(result.InlineResponses);
        Assert.True(response.Metadata.TryGetValue("sessionId", out var sessionId));
        Assert.Equal("session-1", sessionId?.ToString());
        Assert.NotNull(response.Response);
        Assert.Equal("{\"topics\":[]}", response.Response!.Content);
        Assert.Equal(25, response.Response.TokenUsage!.TotalTokens);
        Assert.Equal("flex", response.Response.ServiceTier);
        var promptDetail = Assert.Single(response.Response.TokenUsage.PromptTokenDetails);
        Assert.Equal("TEXT", promptDetail.Modality);
        Assert.Equal(20, promptDetail.TokenCount);
        var cacheDetail = Assert.Single(response.Response.TokenUsage.CachedTokenDetails);
        Assert.Equal("TEXT", cacheDetail.Modality);
        Assert.Equal(5, cacheDetail.TokenCount);
        var candidateDetail = Assert.Single(response.Response.TokenUsage.CandidateTokenDetails);
        Assert.Equal("TEXT", candidateDetail.Modality);
        Assert.Equal(5, candidateDetail.TokenCount);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Equal("/v1beta/batches/batch-123", handler.Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBatchAsync_WithDocumentedInlineResponseShape_ParsesResponsesAndErrors()
    {
        var handler = new CapturingHandler("""
            {
              "name":"batches/batch-456",
              "done":true,
              "metadata":{"state":"JOB_STATE_SUCCEEDED"},
              "response":{
                "inlinedResponses":[
                  {
                    "metadata":{"sessionId":"session-1","userProfileId":"user-1"},
                    "response":{
                      "candidates":[{"content":{"parts":[{"text":"{\"topics\":[\"quote\"]}"}]}}],
                      "usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}
                    }
                  },
                  {
                    "metadata":{"sessionId":"session-2"},
                    "error":{"message":"Request was rejected"}
                  }
                ]
              }
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetBatchAsync("batches/batch-456");

        Assert.Equal("batches/batch-456", result.Name);
        Assert.True(result.Done);
        Assert.Equal("JOB_STATE_SUCCEEDED", result.State);
        Assert.Equal(2, result.InlineResponses.Count);

        var successfulResponse = result.InlineResponses[0];
        Assert.Equal("session-1", successfulResponse.Metadata["sessionId"]?.ToString());
        Assert.Equal("user-1", successfulResponse.Metadata["userProfileId"]?.ToString());
        Assert.NotNull(successfulResponse.Response);
        Assert.Equal("{\"topics\":[\"quote\"]}", successfulResponse.Response!.Content);
        Assert.Equal(10, successfulResponse.Response.TokenUsage!.PromptTokens);
        Assert.Equal(5, successfulResponse.Response.TokenUsage.CompletionTokens);
        Assert.Equal(15, successfulResponse.Response.TokenUsage.TotalTokens);

        var failedResponse = result.InlineResponses[1];
        Assert.Equal("session-2", failedResponse.Metadata["sessionId"]?.ToString());
        Assert.Null(failedResponse.Response);
        Assert.Equal("Request was rejected", failedResponse.ErrorMessage);
    }

    [Fact]
    public async Task GetBatchAsync_WithDocumentedTopLevelDestShape_ParsesStateResponsesAndErrors()
    {
        var handler = new CapturingHandler("""
            {
              "name":"batches/batch-789",
              "done":true,
              "state":"JOB_STATE_SUCCEEDED",
              "dest":{
                "inlinedResponses":[
                  {
                    "metadata":{"sessionId":"session-1","userProfileId":"user-1"},
                    "response":{
                      "candidates":[{"content":{"parts":[{"text":"{\"topics\":[\"support\"]}"}]}}],
                      "usageMetadata":{"promptTokenCount":12,"candidatesTokenCount":6,"totalTokenCount":18}
                    }
                  },
                  {
                    "metadata":{"sessionId":"session-2"},
                    "error":{"message":"Request failed"}
                  }
                ]
              }
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetBatchAsync("batches/batch-789");

        Assert.Equal("batches/batch-789", result.Name);
        Assert.True(result.Done);
        Assert.Equal("JOB_STATE_SUCCEEDED", result.State);
        Assert.Equal(2, result.InlineResponses.Count);

        var successfulResponse = result.InlineResponses[0];
        Assert.Equal("session-1", successfulResponse.Metadata["sessionId"]?.ToString());
        Assert.Equal("user-1", successfulResponse.Metadata["userProfileId"]?.ToString());
        Assert.NotNull(successfulResponse.Response);
        Assert.Equal("{\"topics\":[\"support\"]}", successfulResponse.Response!.Content);
        Assert.Equal(12, successfulResponse.Response.TokenUsage!.PromptTokens);
        Assert.Equal(6, successfulResponse.Response.TokenUsage.CompletionTokens);
        Assert.Equal(18, successfulResponse.Response.TokenUsage.TotalTokens);

        var failedResponse = result.InlineResponses[1];
        Assert.Equal("session-2", failedResponse.Metadata["sessionId"]?.ToString());
        Assert.Null(failedResponse.Response);
        Assert.Equal("Request failed", failedResponse.ErrorMessage);
    }

    [Fact]
    public async Task GetBatchAsync_WithDocumentedResponseBatchShape_ParsesBatchStateAndInlineResponses()
    {
        var handler = new CapturingHandler("""
            {
              "name":"operations/batch-999",
              "done":true,
              "response":{
                "batch":{
                  "name":"batches/batch-999",
                  "state":"BATCH_STATE_SUCCEEDED",
                  "output":{
                    "inlinedResponses":[
                      {
                        "metadata":{"sessionId":"session-1","userProfileId":"user-1"},
                        "response":{
                          "candidates":[{"content":{"parts":[{"text":"{\"topics\":[\"batch\"]}"}]}}],
                          "usageMetadata":{"promptTokenCount":14,"candidatesTokenCount":7,"totalTokenCount":21}
                        }
                      }
                    ]
                  }
                }
              }
            }
            """);
        var client = CreateClient(handler);

        var result = await client.GetBatchAsync("batches/batch-999");

        Assert.Equal("operations/batch-999", result.Name);
        Assert.True(result.Done);
        Assert.Equal("BATCH_STATE_SUCCEEDED", result.State);
        var response = Assert.Single(result.InlineResponses);
        Assert.Equal("session-1", response.Metadata["sessionId"]?.ToString());
        Assert.NotNull(response.Response);
        Assert.Equal("{\"topics\":[\"batch\"]}", response.Response!.Content);
        Assert.Equal(14, response.Response.TokenUsage!.PromptTokens);
        Assert.Equal(7, response.Response.TokenUsage.CompletionTokens);
        Assert.Equal(21, response.Response.TokenUsage.TotalTokens);
    }

    private static GeminiBatchClient CreateClient(
        HttpMessageHandler handler,
        Dictionary<string, string?>? extraConfiguration = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-api-key",
            ["Gemini:MainModelName"] = "gemini-2.5-flash"
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

        return new GeminiBatchClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            configuration,
            NullLogger<GeminiBatchClient>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public CapturingHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SequencedCapturingHandler : HttpMessageHandler
    {
        private readonly Queue<CapturedResponse> _responses;

        public SequencedCapturingHandler(params CapturedResponse[] responses)
        {
            _responses = new Queue<CapturedResponse>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            var response = _responses.Dequeue();
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record CapturedResponse(HttpStatusCode StatusCode, string Body);
}
