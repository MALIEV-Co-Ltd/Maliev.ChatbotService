using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for rate limiting with Redis sliding window (100 messages per hour).
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RateLimitService> _logger;
    private const int MaxMessagesPerHour = 100;
    private readonly TimeSpan _windowDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitService"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger.</param>
    public RateLimitService(IConnectionMultiplexer redis, ILogger<RateLimitService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsRateLimitExceededAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetCacheKey(userProfileId);
        var count = await db.StringGetAsync(key);

        if (!count.HasValue)
        {
            return false;
        }

        return (int)count >= MaxMessagesPerHour;
    }

    /// <inheritdoc/>
    public async Task<int> IncrementMessageCountAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetCacheKey(userProfileId);
        
        // Atomic increment
        var newCount = await db.StringIncrementAsync(key);
        
        // If it's a new key, set expiration
        if (newCount == 1)
        {
            await db.KeyExpireAsync(key, _windowDuration);
        }

        if (newCount >= MaxMessagesPerHour)
        {
            _logger.LogWarning("User {UserProfileId} has exceeded rate limit: {Count}/{Max}", userProfileId, newCount, MaxMessagesPerHour);
        }

        return (int)newCount;
    }

    /// <inheritdoc/>
    public async Task<int> GetRemainingMessagesAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetCacheKey(userProfileId);
        var count = await db.StringGetAsync(key);

        var currentCount = !count.HasValue ? 0 : (int)count;
        var remaining = Math.Max(0, MaxMessagesPerHour - currentCount);

        return remaining;
    }

    /// <inheritdoc/>
    public async Task<(bool IsExceeded, int Remaining)> CheckRateLimitAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = GetCacheKey(userProfileId);
        
        // We use a transaction or Lua script if we wanted absolute atomicity for both check and get remaining, 
        // but for this UI purpose, sequential calls are acceptable.
        var isExceeded = await IsRateLimitExceededAsync(userProfileId, cancellationToken);
        var remaining = await GetRemainingMessagesAsync(userProfileId, cancellationToken);
        return (isExceeded, remaining);
    }

    /// <inheritdoc/>
    public async Task IncrementCountAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        await IncrementMessageCountAsync(userProfileId, cancellationToken);
    }

    private static string GetCacheKey(Guid userProfileId)
    {
        return $"chatbot:ratelimit:{userProfileId}";
    }
}
