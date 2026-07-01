using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RedisExpirySafetyMargin = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<GeminiModelContextCacheService> _logger;
    private readonly string _apiKey;
    private readonly string _modelName;
    private readonly bool _enabled;
    private readonly int _minSystemInstructionCharacters;
    private readonly int? _configuredMinInputTokens;
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
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured. Set 'Gemini:ApiKey'.");
        _modelName = configuration["Gemini:MainModelName"] ?? "gemini-2.5-flash";
        _enabled = configuration.GetValue<bool?>("Gemini:ContextCache:Enabled") ?? true;
        _minSystemInstructionCharacters = Math.Max(
            0,
            configuration.GetValue<int?>("Gemini:ContextCache:MinSystemInstructionCharacters") ?? 8192);
        _configuredMinInputTokens = configuration.GetValue<int?>("Gemini:ContextCache:MinInputTokens");

        var ttlSeconds = Math.Max(60, configuration.GetValue<int?>("Gemini:ContextCache:TtlSeconds") ?? 3600);
        _ttl = TimeSpan.FromSeconds(ttlSeconds);
    }

    /// <inheritdoc/>
    public async Task<ModelContextCacheReference?> GetOrCreateSystemInstructionCacheAsync(
        ModelContextCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled ||
            string.IsNullOrWhiteSpace(request.SystemInstruction) ||
            request.SystemInstruction.Length < _minSystemInstructionCharacters)
        {
            return null;
        }

        var modelName = NormalizeModelName(request.ModelName ?? _modelName);
        var cacheKey = BuildCacheKey(modelName, request.SystemInstruction);
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

                if (!await IsCacheTokenEligibleAsync(modelName, request.SystemInstruction, cancellationToken))
                {
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
                return new ModelContextCacheReference { CachedContentName = createdName };
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

    private async Task<bool> IsCacheTokenEligibleAsync(
        string modelName,
        string systemInstruction,
        CancellationToken cancellationToken)
    {
        var minimumTokens = ResolveMinimumCacheTokens(modelName);
        try
        {
            var tokenCount = await CountSystemInstructionTokensAsync(modelName, systemInstruction, cancellationToken);
            if (tokenCount >= minimumTokens)
            {
                return true;
            }

            _logger.LogDebug(
                "Skipping Gemini context cache for model {ModelName}: {TokenCount} tokens below cache threshold {MinimumTokens}.",
                modelName,
                tokenCount,
                minimumTokens);
            return false;
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
            return false;
        }
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
                "Gemini context cache countTokens returned {StatusCode}: {Content}",
                response.StatusCode,
                responseContent);
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
                "Gemini context cache create returned {StatusCode}: {Content}",
                response.StatusCode,
                responseContent);
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
