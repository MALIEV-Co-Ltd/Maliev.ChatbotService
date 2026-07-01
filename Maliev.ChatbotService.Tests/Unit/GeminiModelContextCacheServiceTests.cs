using System.Net;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiModelContextCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateSystemInstructionCacheAsync_ExistingRedisName_ReusesCacheWithoutGeminiCall()
    {
        var handler = new CapturingHandler("""{"name":"cachedContents/new"}""");
        var database = CreateRedisDatabase();
        database
            .Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync("cachedContents/reused");
        var service = CreateService(handler, database.Object);

        var result = await service.GetOrCreateSystemInstructionCacheAsync(new ModelContextCacheRequest
        {
            ModelName = "gemini-2.5-flash",
            SystemInstruction = new string('x', 9000)
        });

        Assert.NotNull(result);
        Assert.Equal("cachedContents/reused", result!.CachedContentName);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetOrCreateSystemInstructionCacheAsync_LargeInstruction_CreatesCacheAndStoresName()
    {
        var handler = new CapturingHandler(
            """{"totalTokens":2048}""",
            """{"name":"cachedContents/created"}""");
        var database = CreateRedisDatabase();
        database
            .Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        database
            .Setup(item => item.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        RedisKey storedKey = default;
        RedisValue storedValue = default;
        TimeSpan? storedExpiry = null;
        database
            .Setup(item => item.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, expiry, _, _, _) =>
            {
                storedKey = key;
                storedValue = value;
                storedExpiry = expiry;
            })
            .ReturnsAsync(true);

        var service = CreateService(handler, database.Object);
        var systemInstruction = new string('x', 9000);

        var result = await service.GetOrCreateSystemInstructionCacheAsync(new ModelContextCacheRequest
        {
            ModelName = "gemini-2.5-flash",
            SystemInstruction = systemInstruction
        });

        Assert.NotNull(result);
        Assert.Equal("cachedContents/created", result!.CachedContentName);
        Assert.Equal("cachedContents/created", storedValue.ToString());
        Assert.StartsWith("chatbot:gemini:context-cache:v1:system-instruction:gemini-2.5-flash:", storedKey.ToString());
        Assert.NotNull(storedExpiry);
        Assert.True(storedExpiry < TimeSpan.FromSeconds(3600));

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(
            ["/v1beta/models/gemini-2.5-flash:countTokens", "/v1beta/cachedContents"],
            handler.RequestUris);
        Assert.All(handler.Requests, request => Assert.True(request.Headers.Contains("x-goog-api-key")));

        using var countPayload = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal(
            systemInstruction,
            countPayload.RootElement
                .GetProperty("generateContentRequest")
                .GetProperty("systemInstruction")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString());

        using var payload = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.Equal("models/gemini-2.5-flash", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("3600s", payload.RootElement.GetProperty("ttl").GetString());
        Assert.Equal(
            systemInstruction,
            payload.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task GetOrCreateSystemInstructionCacheAsync_ShortCharactersButTokenEligible_CreatesCache()
    {
        var handler = new CapturingHandler(
            """{"totalTokens":2048}""",
            """{"name":"cachedContents/token-eligible"}""");
        var database = CreateRedisDatabase();
        database
            .Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        database
            .Setup(item => item.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService(handler, database.Object);

        var result = await service.GetOrCreateSystemInstructionCacheAsync(new ModelContextCacheRequest
        {
            ModelName = "gemini-2.5-flash",
            SystemInstruction = new string('ก', 4096)
        });

        Assert.NotNull(result);
        Assert.Equal("cachedContents/token-eligible", result!.CachedContentName);
        Assert.Equal(
            ["/v1beta/models/gemini-2.5-flash:countTokens", "/v1beta/cachedContents"],
            handler.RequestUris);
    }

    [Fact]
    public async Task GetOrCreateSystemInstructionCacheAsync_BelowTokenThreshold_SkipsCacheCreate()
    {
        var handler = new CapturingHandler("""{"totalTokens":2047}""");
        var database = CreateRedisDatabase();
        database
            .Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        database
            .Setup(item => item.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService(handler, database.Object);

        var result = await service.GetOrCreateSystemInstructionCacheAsync(new ModelContextCacheRequest
        {
            ModelName = "gemini-2.5-flash",
            SystemInstruction = new string('x', 9000)
        });

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/v1beta/models/gemini-2.5-flash:countTokens", Assert.Single(handler.RequestUris));
        database.Verify(
            item => item.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateSystemInstructionCacheAsync_Gemini35BelowModelThreshold_SkipsCacheCreate()
    {
        var handler = new CapturingHandler("""{"totalTokens":4095}""");
        var database = CreateRedisDatabase();
        database
            .Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        database
            .Setup(item => item.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService(handler, database.Object);

        var result = await service.GetOrCreateSystemInstructionCacheAsync(new ModelContextCacheRequest
        {
            ModelName = "gemini-3.5-flash",
            SystemInstruction = new string('x', 9000)
        });

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/v1beta/models/gemini-3.5-flash:countTokens", Assert.Single(handler.RequestUris));
    }

    [Fact]
    public async Task GetOrCreateSystemInstructionCacheAsync_ShortInstructionBelowTokenThreshold_SkipsCacheCreate()
    {
        var handler = new CapturingHandler("""{"totalTokens":12}""");
        var database = CreateRedisDatabase();
        database
            .Setup(item => item.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        database
            .Setup(item => item.LockTakeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        database
            .Setup(item => item.LockReleaseAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var service = CreateService(handler, database.Object);

        var result = await service.GetOrCreateSystemInstructionCacheAsync(new ModelContextCacheRequest
        {
            ModelName = "gemini-2.5-flash",
            SystemInstruction = "short"
        });

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("/v1beta/models/gemini-2.5-flash:countTokens", Assert.Single(handler.RequestUris));
        database.Verify(
            item => item.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    private static GeminiModelContextCacheService CreateService(CapturingHandler handler, IDatabase database)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-api-key",
                ["Gemini:MainModelName"] = "gemini-2.5-flash",
                ["Gemini:ContextCache:TtlSeconds"] = "3600"
            })
            .Build();
        var redis = new Mock<IConnectionMultiplexer>();
        redis
            .Setup(item => item.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database);

        return new GeminiModelContextCacheService(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
            configuration,
            redis.Object,
            NullLogger<GeminiModelContextCacheService>.Instance);
    }

    private static Mock<IDatabase> CreateRedisDatabase()
    {
        return new Mock<IDatabase>();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responseJson;

        public CapturingHandler(params string[] responseJson)
        {
            _responseJson = new Queue<string>(responseJson);
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestUris { get; } = [];

        public List<string> RequestBodies { get; } = [];

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Request = request;
            Requests.Add(request);
            RequestUris.Add(request.RequestUri!.AbsolutePath);
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (RequestBody is not null)
            {
                RequestBodies.Add(RequestBody);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }
}
