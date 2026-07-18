using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ModelProviderErrorSanitizationTests
{
    private const string SensitiveProviderBody = """
        {"error":{"message":"Jane Customer +66898950690 asked to quote drawing 36/1 Moo 3"}}
        """;

    [Fact]
    public async Task GeminiClient_SendMessageProviderError_DoesNotLogRawProviderBody()
    {
        var logger = new CapturingLogger<GeminiClient>();
        var configuration = CreateGeminiConfiguration();
        var client = new GeminiClient(
            new HttpClient(new StatusHandler(HttpStatusCode.BadRequest, SensitiveProviderBody))
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
            },
            configuration,
            new ConversationMetrics(CreateMeterFactory(), configuration),
            logger);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Prompt = "Quote Jane Customer's drawing."
        });

        Assert.False(response.Success);
        AssertLoggedWithoutSensitiveBody(logger);
    }

    [Fact]
    public async Task OpenAiCompatibleClient_SendMessageProviderError_DoesNotLogRawProviderBody()
    {
        var logger = new CapturingLogger<OpenAICompatibleModelProviderClient>();
        var client = new OpenAICompatibleModelProviderClient(
            new HttpClient(new StatusHandler(HttpStatusCode.BadRequest, SensitiveProviderBody))
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
            },
            CreateOpenAiCompatibleConfiguration(),
            logger);

        var response = await client.SendMessageAsync(new GeminiRequest
        {
            Messages = [new GeminiMessage { Role = "user", Content = "Quote Jane Customer's drawing." }]
        });

        Assert.False(response.Success);
        AssertLoggedWithoutSensitiveBody(logger);
    }

    [Fact]
    public async Task GeminiBatchClient_CreateBatchProviderError_DoesNotLogRawProviderBody()
    {
        var logger = new CapturingLogger<GeminiBatchClient>();
        var client = new GeminiBatchClient(
            new HttpClient(new StatusHandler(HttpStatusCode.BadRequest, SensitiveProviderBody))
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
            },
            CreateGeminiConfiguration(),
            logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateInlineGenerateContentBatchAsync(new ModelBatchRequest
            {
                DisplayName = "test-batch",
                ModelName = "gemini-2.5-flash-lite",
                Requests =
                [
                    new ModelBatchGenerateContentRequest
                    {
                        Request = new GeminiRequest
                        {
                            Prompt = "Summarize private customer context.",
                            MaxTokens = 128
                        }
                    }
                ]
            }));

        AssertLoggedWithoutSensitiveBody(logger);
    }

    [Fact]
    public async Task GeminiFileStaging_StartUploadProviderError_DoesNotIncludeRawProviderBodyInException()
    {
        var client = new GeminiModelFileStagingService(
            new HttpClient(new StatusHandler(HttpStatusCode.BadRequest, SensitiveProviderBody))
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
            },
            CreateGeminiConfiguration(),
            new CapturingLogger<GeminiModelFileStagingService>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StageFileAsync(new ModelFileStagingRequest
            {
                FileName = "customer-drawing.pdf",
                MimeType = "application/pdf",
                Content = "private drawing bytes"u8.ToArray()
            }));

        Assert.DoesNotContain("Jane Customer", exception.Message);
        Assert.DoesNotContain("+66898950690", exception.Message);
        Assert.DoesNotContain("36/1 Moo 3", exception.Message);
        Assert.Contains("400", exception.Message);
    }

    private static void AssertLoggedWithoutSensitiveBody<T>(CapturingLogger<T> logger)
    {
        var logs = string.Join('\n', logger.Messages);
        Assert.DoesNotContain("Jane Customer", logs);
        Assert.DoesNotContain("+66898950690", logs);
        Assert.DoesNotContain("36/1 Moo 3", logs);
        Assert.Contains("BadRequest", logs);
    }

    private static IConfiguration CreateGeminiConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key",
                ["Gemini:MainModelName"] = "gemini-2.5-flash"
            })
            .Build();
    }

    private static IConfiguration CreateOpenAiCompatibleConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:OpenAICompatible:ApiKey"] = "test-key",
                ["Llm:OpenAICompatible:ModelName"] = "gemini-2.5-flash"
            })
            .Build();
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var factory = new Mock<IMeterFactory>();
        factory
            .Setup(item => item.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("test-provider-error-sanitization"));
        return factory.Object;
    }

    private sealed class StatusHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
