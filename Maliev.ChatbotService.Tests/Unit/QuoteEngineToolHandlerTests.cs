using System.Net;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Tools;
using Maliev.ChatbotService.Infrastructure.Tools.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class QuoteEngineToolHandlerTests
{
    public static IEnumerable<object[]> DeclaredQuoteEngineTools =>
        ToolRegistry.GetToolDeclarationsForProfile("quote-engine")
            .SelectMany(declaration => declaration.FunctionDeclarations ?? [])
            .Select(declaration => new object[] { declaration.Name });

    [Fact]
    public async Task ExecuteAsync_QuoteApproveQuote_ForwardsSignedContextToQuoteEngineBff()
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var quoteEngineToolHandler = new QuoteEngineToolHandler(factory.Object);

        var result = await quoteEngineToolHandler.ExecuteAsync(
            "quote_approve_quote",
            new Dictionary<string, object>(),
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal(HttpMethod.Post, handler.SentRequest.Method);
        Assert.Equal("/quote/v1/agent/tools/quote_approve_quote", handler.SentRequest.RequestUri?.PathAndQuery);
        Assert.True(handler.SentRequest.Headers.TryGetValues("X-Maliev-Agent-Context", out var values));
        Assert.Contains("signed-context-token", values);
    }

    [Fact]
    public async Task ExecuteAsync_QuoteApproveQuote_RoutesThroughToolExecutor()
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var executor = new ToolExecutorService(factory.Object, NullLogger<ToolExecutorService>.Instance);

        var result = await executor.ExecuteAsync(
            "quote_approve_quote",
            new Dictionary<string, object>(),
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal("/quote/v1/agent/tools/quote_approve_quote", handler.SentRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ExecuteAsync_QuoteResumeProject_ForwardsSignedContextToQuoteEngineBff()
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var quoteEngineToolHandler = new QuoteEngineToolHandler(factory.Object);

        var result = await quoteEngineToolHandler.ExecuteAsync(
            "quote_resume_project",
            new Dictionary<string, object>
            {
                ["project_id"] = "11111111-1111-1111-1111-111111111111"
            },
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal(HttpMethod.Post, handler.SentRequest.Method);
        Assert.Equal("/quote/v1/agent/tools/quote_resume_project", handler.SentRequest.RequestUri?.PathAndQuery);
        Assert.True(handler.SentRequest.Headers.TryGetValues("X-Maliev-Agent-Context", out var values));
        Assert.Contains("signed-context-token", values);
    }

    [Fact]
    public async Task ExecuteAsync_QuoteResumeProject_RoutesThroughToolExecutor()
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var executor = new ToolExecutorService(factory.Object, NullLogger<ToolExecutorService>.Instance);

        var result = await executor.ExecuteAsync(
            "quote_resume_project",
            new Dictionary<string, object>
            {
                ["project_id"] = "11111111-1111-1111-1111-111111111111"
            },
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal("/quote/v1/agent/tools/quote_resume_project", handler.SentRequest.RequestUri?.PathAndQuery);
    }

    [Theory]
    [MemberData(nameof(DeclaredQuoteEngineTools))]
    public async Task ExecuteAsync_DeclaredQuoteEngineTool_RoutesThroughToolExecutor(string toolName)
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var executor = new ToolExecutorService(factory.Object, NullLogger<ToolExecutorService>.Instance);

        var result = await executor.ExecuteAsync(
            toolName,
            new Dictionary<string, object>(),
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal(HttpMethod.Post, handler.SentRequest.Method);
        Assert.Equal($"/quote/v1/agent/tools/{toolName}", handler.SentRequest.RequestUri?.PathAndQuery);
        Assert.True(handler.SentRequest.Headers.TryGetValues("X-Maliev-Agent-Context", out var values));
        Assert.Contains("signed-context-token", values);
    }

    [Theory]
    [InlineData("quote_get_project_summary", "focus", "next_steps")]
    [InlineData("quote_get_connectors", "category", "file_import")]
    [InlineData("quote_get_connector_handoff", "connector_id", "google-drive")]
    [InlineData("quote_register_uploads", "requirements", "Quote this uploaded STEP file.")]
    [InlineData("quote_search_customer_data", "query", "fixture")]
    [InlineData("quote_get_auth_handoff", "return_url", "/quote/new?checkout=1")]
    [InlineData("quote_update_settings", "currency", "THB")]
    [InlineData("quote_calculate_estimate", "currency", "THB")]
    [InlineData("quote_duplicate_project", "title", "Duplicate from chat")]
    [InlineData("quote_pin_project", "project_id", "11111111-1111-1111-1111-111111111111")]
    [InlineData("quote_unpin_project", "project_id", "11111111-1111-1111-1111-111111111111")]
    [InlineData("quote_archive_project", "project_id", "11111111-1111-1111-1111-111111111111")]
    [InlineData("quote_achieve_project", "project_id", "11111111-1111-1111-1111-111111111111")]
    [InlineData("quote_update_checkout_details", "billing_address_id", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    public async Task ExecuteAsync_QuoteProjectWorkflowTool_ForwardsSignedContextAndArgumentsToQuoteEngineBff(
        string toolName,
        string argumentName,
        string argumentValue)
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var quoteEngineToolHandler = new QuoteEngineToolHandler(factory.Object);

        var result = await quoteEngineToolHandler.ExecuteAsync(
            toolName,
            new Dictionary<string, object>
            {
                [argumentName] = argumentValue
            },
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal(HttpMethod.Post, handler.SentRequest.Method);
        Assert.Equal($"/quote/v1/agent/tools/{toolName}", handler.SentRequest.RequestUri?.PathAndQuery);
        Assert.True(handler.SentRequest.Headers.TryGetValues("X-Maliev-Agent-Context", out var values));
        Assert.Contains("signed-context-token", values);

        Assert.False(string.IsNullOrWhiteSpace(handler.SentContent));
        using var document = JsonDocument.Parse(handler.SentContent);
        Assert.Equal(argumentValue, document.RootElement.GetProperty("arguments").GetProperty(argumentName).GetString());
    }

    [Theory]
    [InlineData("quote_get_project_summary")]
    [InlineData("quote_get_connectors")]
    [InlineData("quote_get_connector_handoff")]
    [InlineData("quote_register_uploads")]
    [InlineData("quote_search_customer_data")]
    [InlineData("quote_get_auth_handoff")]
    [InlineData("quote_get_settings")]
    [InlineData("quote_update_settings")]
    [InlineData("quote_calculate_estimate")]
    [InlineData("quote_duplicate_project")]
    [InlineData("quote_pin_project")]
    [InlineData("quote_unpin_project")]
    [InlineData("quote_archive_project")]
    [InlineData("quote_achieve_project")]
    [InlineData("quote_update_checkout_details")]
    public async Task ExecuteAsync_QuoteProjectWorkflowTool_RoutesThroughToolExecutor(string toolName)
    {
        var handler = new CapturingQuoteEngineHandler();
        var factory = CreateFactory(handler);
        var executor = new ToolExecutorService(factory.Object, NullLogger<ToolExecutorService>.Instance);

        var result = await executor.ExecuteAsync(
            toolName,
            new Dictionary<string, object>(),
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal("{\"ok\":true}", result);
        Assert.NotNull(handler.SentRequest);
        Assert.Equal($"/quote/v1/agent/tools/{toolName}", handler.SentRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ExecuteAsync_QuoteCalculateEstimate_ReturnsGateErrorPayloadUnchanged()
    {
        const string gateErrorJson = """
            {"error":"Upload STEP before pricing.","requiredGateCode":"geometry_required","actionType":"calculate_estimate","state":{"estimate":null}}
            """;
        var handler = new CapturingQuoteEngineHandler(gateErrorJson);
        var factory = CreateFactory(handler);
        var quoteEngineToolHandler = new QuoteEngineToolHandler(factory.Object);

        var result = await quoteEngineToolHandler.ExecuteAsync(
            "quote_calculate_estimate",
            new Dictionary<string, object>(),
            new ToolExecutionContext(null, "signed-context-token"),
            CancellationToken.None);

        Assert.Equal(gateErrorJson, result);
        Assert.Equal("/quote/v1/agent/tools/quote_calculate_estimate", handler.SentRequest?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ExecuteAsync_QuoteEngineBffReturnsFailure_RedactsDownstreamBody()
    {
        const string downstreamError = """
            {"error":"Invalid session","debug":"user@example.com stack trace secret-token"}
            """;
        var handler = new CapturingQuoteEngineHandler(downstreamError, HttpStatusCode.InternalServerError);
        var factory = CreateFactory(handler);
        var quoteEngineToolHandler = new QuoteEngineToolHandler(factory.Object);

        var result = await quoteEngineToolHandler.ExecuteAsync(
            "quote_start_payment",
            new Dictionary<string, object>(),
            new ToolExecutionContext("customer-token", "signed-context-token"),
            CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.Equal("QuoteEngine BFF returned 500", document.RootElement.GetProperty("error").GetString());
        Assert.Equal("quote_start_payment", document.RootElement.GetProperty("tool").GetString());
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain("user@example.com", result);
        Assert.DoesNotContain("secret-token", result);
        Assert.DoesNotContain("stack trace", result);
    }

    private static Mock<IHttpClientFactory> CreateFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("QuoteEngineBff"))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://quote-engine.test") });
        return factory;
    }

    private sealed class CapturingQuoteEngineHandler(
        string responseContent = "{\"ok\":true}",
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? SentRequest { get; private set; }
        public string? SentContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SentRequest = request;
            SentContent = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent)
            };
        }
    }
}
