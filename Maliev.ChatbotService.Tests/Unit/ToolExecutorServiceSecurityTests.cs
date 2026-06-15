using System.Net;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ToolExecutorServiceSecurityTests
{
    [Fact]
    public async Task ExecuteAsync_SensitiveToolArguments_DoesNotWriteArgumentValuesToLogs()
    {
        var logger = new Mock<ILogger<ToolExecutorService>>();
        var handler = new OkResponseHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("QuoteEngineBff"))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("http://quote-engine.test") });
        var executor = new ToolExecutorService(factory.Object, logger.Object);

        await executor.ExecuteAsync(
            "quote_update_checkout_details",
            new Dictionary<string, object>
            {
                ["phone"] = "+66818030404",
                ["company"] = "Sensitive Customer Co.",
                ["vat_number"] = "VAT-SECRET-123",
                ["billing_address_id"] = "billing-address-secret",
                ["shipping_address_id"] = "shipping-address-secret",
                ["accepted_terms"] = true,
                ["consent"] = true
            },
            new ToolExecutionContext("customer-token", "signed-context-token"),
            CancellationToken.None);

        logger.Verify(
            item => item.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    !state.ToString()!.Contains("+66818030404", StringComparison.Ordinal)
                    && !state.ToString()!.Contains("Sensitive Customer Co.", StringComparison.Ordinal)
                    && !state.ToString()!.Contains("VAT-SECRET-123", StringComparison.Ordinal)
                    && !state.ToString()!.Contains("billing-address-secret", StringComparison.Ordinal)
                    && !state.ToString()!.Contains("shipping-address-secret", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private sealed class OkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        }
    }
}
