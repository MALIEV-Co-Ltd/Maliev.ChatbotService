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
    [InlineData("quote_get_connectors", "category", "file_import")]
    [InlineData("quote_register_uploads", "requirements", "Quote this uploaded STEP file.")]
    [InlineData("quote_search_customer_data", "query", "fixture")]
    [InlineData("quote_duplicate_project", "title", "Duplicate from chat")]
    [InlineData("quote_pin_project", "project_id", "11111111-1111-1111-1111-111111111111")]
    [InlineData("quote_archive_project", "project_id", "11111111-1111-1111-1111-111111111111")]
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
    [InlineData("quote_get_connectors")]
    [InlineData("quote_register_uploads")]
    [InlineData("quote_search_customer_data")]
    [InlineData("quote_duplicate_project")]
    [InlineData("quote_pin_project")]
    [InlineData("quote_archive_project")]
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

    private static Mock<IHttpClientFactory> CreateFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("QuoteEngineBff"))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://quote-engine.test") });
        return factory;
    }

    private sealed class CapturingQuoteEngineHandler : HttpMessageHandler
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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        }
    }
}
