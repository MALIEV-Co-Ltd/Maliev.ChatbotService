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
        Assert.Equal("expired-session-summaries", batch.GetProperty("display_name").GetString());
        Assert.Equal("-10", batch.GetProperty("priority").GetString());

        var inlineRequest = batch
            .GetProperty("input_config")
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

    private static GeminiBatchClient CreateClient(CapturingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-api-key",
                ["Gemini:MainModelName"] = "gemini-2.5-flash"
            })
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
}
