using System.Net;
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SentRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        }
    }
}
