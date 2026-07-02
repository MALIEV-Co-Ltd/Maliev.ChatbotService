using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Costing;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Manages Gemini explicit context caches for stable system instruction prefixes.
/// </summary>
public sealed class GeminiModelContextCacheService : IModelContextCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string CacheKeyPrefix = "chatbot:gemini:context-cache:v1:system-instruction:";
    private const long DefaultMaxStorageCostMicroUsd = 50_000;
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RedisExpirySafetyMargin = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<GeminiModelContextCacheService> _logger;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly bool _enabled;
    private readonly int? _configuredMinInputTokens;
    private readonly long _maxStorageCostMicroUsd;
    private readonly TimeSpan _ttl;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiModelContextCacheService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger.</param>
    public GeminiModelContextCacheService(
        HttpClient httpClient,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        ILogger<GeminiModelContextCacheService> logger)
    {
        _httpClient = httpClient;
        _redis = redis;
        _logger = logger;
        _apiKey = GeminiApiConfiguration.ResolveApiKey(configuration);
        _modelName = GeminiApiConfiguration.ResolveMainModelName(configuration);
        _enabled = configuration.GetValue<bool?>("Gemini:ContextCache:Enabled") ?? true;
        _configuredMinInputTokens = configuration.GetValue<int?>("Gemini:ContextCache:MinInputTokens");
        _maxStorageCostMicroUsd = Math.Max(
            0,
            configuration.GetValue<long?>("Gemini:ContextCache:MaxStorageCostMicroUsd") ?? DefaultMaxStorageCostMicroUsd);

        var ttlSeconds = Math.Max(60, configuration.GetValue<int?>("Gemini:ContextCache:TtlSeconds") ?? 3600);
        _ttl = TimeSpan.FromSeconds(ttlSeconds);
    }

    /// <inheritdoc/>
    public async Task<ModelContextCacheReference?> GetOrCreateSystemInstructionCacheAsync(
        ModelContextCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled ||
            string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            return null;
        }

        var modelName = NormalizeModelName(request.ModelName ?? _modelName);
        var cacheKey = BuildCacheKey(modelName, request.SystemInstruction);
        var minimumTokens = ResolveMinimumCacheTokens(modelName);
        var ineligibleKey = BuildIneligibleCacheKey(cacheKey, minimumTokens);
        var lockKey = $"{cacheKey}:lock";
        var lockValue = Guid.NewGuid().ToString("N");

        try
        {
            var db = _redis.GetDatabase();
            var cachedName = await db.StringGetAsync(cacheKey);
            if (cachedName.HasValue)
            {
                return new ModelContextCacheReference { CachedContentName = cachedName.ToString() };
            }

            var ineligibleMarker = await db.StringGetAsync(ineligibleKey);
            if (ineligibleMarker.HasValue)
            {
                return null;
            }

            var storageIneligibleKey = BuildStorageIneligibleCacheKey(cacheKey, _ttl, _maxStorageCostMicroUsd);
            var storageIneligibleMarker = await db.StringGetAsync(storageIneligibleKey);
            if (storageIneligibleMarker.HasValue)
            {
                return null;
            }

            if (!await db.LockTakeAsync(lockKey, lockValue, LockExpiry))
            {
                return null;
            }

            try
            {
                cachedName = await db.StringGetAsync(cacheKey);
                if (cachedName.HasValue)
                {
                    return new ModelContextCacheReference { CachedContentName = cachedName.ToString() };
                }

                ineligibleMarker = await db.StringGetAsync(ineligibleKey);
                if (ineligibleMarker.HasValue)
                {
                    return null;
                }

                storageIneligibleMarker = await db.StringGetAsync(storageIneligibleKey);
                if (storageIneligibleMarker.HasValue)
                {
                    return null;
                }

                var cacheTokenCount = await GetEligibleCacheTokenCountAsync(
                    modelName,
                    request.SystemInstruction,
                    minimumTokens,
                    cancellationToken);
                if (cacheTokenCount <= 0)
                {
                    await db.StringSetAsync(
                        ineligibleKey,
                        "1",
                        ResolveRedisExpiry(),
                        keepTtl: false,
                        when: When.Always,
                        flags: CommandFlags.None);
                    return null;
                }

                var storageCostEstimate = GeminiCostEstimator.EstimateContextCacheStorage(modelName, cacheTokenCount, _ttl);
                if (!IsStorageCostAllowed(modelName, cacheTokenCount, storageCostEstimate))
                {
                    await db.StringSetAsync(
                        storageIneligibleKey,
                        "1",
                        ResolveRedisExpiry(),
                        keepTtl: false,
                        when: When.Always,
                        flags: CommandFlags.None);
                    return null;
                }

                var createdName = await CreateGeminiCacheAsync(modelName, request.SystemInstruction, cancellationToken);
                if (string.IsNullOrWhiteSpace(createdName))
                {
                    return null;
                }

                await db.StringSetAsync(
                    cacheKey,
                    createdName,
                    ResolveRedisExpiry(),
                    keepTtl: false,
                    when: When.Always,
                    flags: CommandFlags.None);
                _logger.LogInformation(
                    "Created Gemini context cache {CacheName} for model {ModelName} with {CachedInputTokens} input tokens, TTL {TtlSeconds}s, estimated storage cost {StorageCostMicroUsd} micro-USD.",
                    createdName,
                    modelName,
                    cacheTokenCount,
                    (int)_ttl.TotalSeconds,
                    storageCostEstimate?.StorageMicroUsd ?? 0);
                return new ModelContextCacheReference
                {
                    CachedContentName = createdName,
                    CachedInputTokens = cacheTokenCount,
                    StorageCostEstimate = storageCostEstimate
                };
            }
            finally
            {
                await db.LockReleaseAsync(lockKey, lockValue);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini context cache lookup/create failed; continuing without explicit cache.");
            return null;
        }
    }

    private async Task<int> GetEligibleCacheTokenCountAsync(
        string modelName,
        string systemInstruction,
        int minimumTokens,
        CancellationToken cancellationToken)
    {
        try
        {
            var tokenCount = await CountSystemInstructionTokensAsync(modelName, systemInstruction, cancellationToken);
            if (tokenCount >= minimumTokens)
            {
                return tokenCount;
            }

            _logger.LogDebug(
                "Skipping Gemini context cache for model {ModelName}: {TokenCount} tokens below cache threshold {MinimumTokens}.",
                modelName,
                tokenCount,
                minimumTokens);
            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Gemini context cache token count failed for model {ModelName}; continuing without explicit cache.",
                modelName);
            return 0;
        }
    }

    private bool IsStorageCostAllowed(
        string modelName,
        int cacheTokenCount,
        GeminiContextCacheStorageEstimate? storageCostEstimate)
    {
        if (_maxStorageCostMicroUsd <= 0)
        {
            return true;
        }

        if (storageCostEstimate is null)
        {
            _logger.LogWarning(
                "Skipping Gemini context cache for model {ModelName}: storage cost could not be estimated for {CachedInputTokens} cached input tokens.",
                modelName,
                cacheTokenCount);
            return false;
        }

        if (storageCostEstimate.StorageMicroUsd <= _maxStorageCostMicroUsd)
        {
            return true;
        }

        _logger.LogInformation(
            "Skipping Gemini context cache for model {ModelName}: estimated storage cost {StorageCostMicroUsd} micro-USD exceeds configured cap {MaxStorageCostMicroUsd} micro-USD.",
            modelName,
            storageCostEstimate.StorageMicroUsd,
            _maxStorageCostMicroUsd);
        return false;
    }

    private async Task<int> CountSystemInstructionTokensAsync(
        string modelName,
        string systemInstruction,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["generateContentRequest"] = new Dictionary<string, object?>
            {
                ["model"] = GeminiClient.ToGeminiModelResourceName(modelName),
                ["contents"] = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = "." } }
                    }
                },
                ["systemInstruction"] = new { parts = new[] { new { text = systemInstruction } } }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{modelName}:countTokens");
        message.Headers.Add("x-goog-api-key", _apiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini context cache countTokens returned {StatusCode}: {ErrorSummary}",
                response.StatusCode,
                ModelProviderErrorSanitizer.Summarize(response, responseContent));
            throw new InvalidOperationException("Gemini context cache countTokens failed.");
        }

        using var document = JsonDocument.Parse(responseContent);
        return document.RootElement.TryGetProperty("totalTokens", out var totalTokens)
            ? totalTokens.GetInt32()
            : 0;
    }

    private async Task<string?> CreateGeminiCacheAsync(
        string modelName,
        string systemInstruction,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = $"models/{modelName}",
            ["displayName"] = $"chatbot-system-{BuildShortHash(modelName, systemInstruction)}",
            ["systemInstruction"] = new { parts = new[] { new { text = systemInstruction } } },
            ["ttl"] = $"{(int)_ttl.TotalSeconds}s"
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1beta/cachedContents");
        message.Headers.Add("x-goog-api-key", _apiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable ||
            !response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Gemini context cache create returned {StatusCode}: {ErrorSummary}",
                response.StatusCode,
                ModelProviderErrorSanitizer.Summarize(response, responseContent));
            return null;
        }

        using var document = JsonDocument.Parse(responseContent);
        return document.RootElement.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : null;
    }

    private TimeSpan ResolveRedisExpiry()
    {
        return _ttl > RedisExpirySafetyMargin + TimeSpan.FromSeconds(1)
            ? _ttl - RedisExpirySafetyMargin
            : _ttl;
    }

    private static string NormalizeModelName(string modelName)
    {
        return modelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelName["models/".Length..]
            : modelName;
    }

    private int ResolveMinimumCacheTokens(string modelName)
    {
        if (_configuredMinInputTokens is > 0)
        {
            return _configuredMinInputTokens.Value;
        }

        return modelName.StartsWith("gemini-3.", StringComparison.OrdinalIgnoreCase)
            ? 4096
            : 2048;
    }

    private static RedisKey BuildCacheKey(string modelName, string systemInstruction)
    {
        var hash = BuildHash($"{modelName}\n{systemInstruction}");
        return $"{CacheKeyPrefix}{modelName}:{hash}";
    }

    private static RedisKey BuildIneligibleCacheKey(RedisKey cacheKey, int minimumTokens)
    {
        return $"{cacheKey}:ineligible:{minimumTokens}";
    }

    private static RedisKey BuildStorageIneligibleCacheKey(
        RedisKey cacheKey,
        TimeSpan ttl,
        long maxStorageCostMicroUsd)
    {
        return $"{cacheKey}:storage-ineligible:{(int)ttl.TotalSeconds}:{maxStorageCostMicroUsd}";
    }

    private static string BuildShortHash(string modelName, string systemInstruction)
    {
        return BuildHash($"{modelName}\n{systemInstruction}")[..16].ToLowerInvariant();
    }

    private static string BuildHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
