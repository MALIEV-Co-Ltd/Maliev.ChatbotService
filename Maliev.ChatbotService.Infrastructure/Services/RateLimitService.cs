using Maliev.ChatbotService.Application.Interfaces;
using Maliev.Aspire.ServiceDefaults.Caching;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for rate limiting with Redis sliding window (100 messages per hour).
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly ICacheService _cache;
    private readonly ILogger<RateLimitService> _logger;
    private const int MaxMessagesPerHour = 100;
    private readonly TimeSpan _windowDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitService"/> class.
    /// </summary>
    /// <param name="cache">The standardized cache service.</param>
    /// <param name="logger">The logger.</param>
    public RateLimitService(ICacheService cache, ILogger<RateLimitService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsRateLimitExceededAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(userProfileId);
        var countString = await _cache.GetAsync<string>(key, cancellationToken);

        if (string.IsNullOrEmpty(countString))
        {
            return false;
        }

        var count = int.Parse(countString);
        return count >= MaxMessagesPerHour;
    }

    /// <inheritdoc/>
    public async Task<int> IncrementMessageCountAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(userProfileId);
        var countString = await _cache.GetAsync<string>(key, cancellationToken);

        var currentCount = (string.IsNullOrEmpty(countString) || !int.TryParse(countString, out var count)) ? 0 : count;
        var newCount = currentCount + 1;

        await _cache.SetAsync(key, newCount.ToString(), _windowDuration, cancellationToken);

        if (newCount >= MaxMessagesPerHour)
        {
            _logger.LogWarning("User {UserProfileId} has exceeded rate limit: {Count}/{Max}", userProfileId, newCount, MaxMessagesPerHour);
        }

        return newCount;
    }

    /// <inheritdoc/>
    public async Task<int> GetRemainingMessagesAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var key = GetCacheKey(userProfileId);
        var countString = await _cache.GetAsync<string>(key, cancellationToken);

        var currentCount = (string.IsNullOrEmpty(countString) || !int.TryParse(countString, out var count)) ? 0 : count;
        var remaining = Math.Max(0, MaxMessagesPerHour - currentCount);

        return remaining;
    }

    /// <inheritdoc/>
    public async Task<(bool IsExceeded, int Remaining)> CheckRateLimitAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
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
