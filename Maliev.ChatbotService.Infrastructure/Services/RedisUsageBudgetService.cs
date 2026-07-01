using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Redis-backed daily token budget (S2). Tracks cumulative model token usage per user in a rolling
/// 24-hour window and enforces a soft ceiling. The window is implemented exactly like the per-hour
/// message rate limiter: a counter whose TTL is set only when the key is first created, so usage
/// rolls off 24 hours after the first recorded token of the window.
/// </summary>
public class RedisUsageBudgetService : IUsageBudgetService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisUsageBudgetService> _logger;
    private readonly long _dailyTokenBudget;

    private const string KeyPrefix = "chatbot:tokenbudget:";

    // Default daily budget per user. Deliberately a high code default (not in appsettings.json) so the
    // integration suite — which records only ~100 mock tokens per message — never trips it without
    // explicitly overriding the setting (the 38-spurious-429 lesson from the per-IP limiter). Set the
    // configured value to 0 to disable the budget entirely.
    private const long DefaultDailyTokenBudget = 2_000_000L;

    private static readonly long WindowMilliseconds = (long)TimeSpan.FromHours(24).TotalMilliseconds;

    // Atomic increment-by-N that sets the rolling-window TTL only when the key is newly created. A
    // non-positive amount never mutates the key (so a zero-token turn cannot reset the TTL); it just
    // returns the current value.
    private const string IncrementWithExpiryScript = @"
        local amount = tonumber(ARGV[1])
        if amount <= 0 then
            local current = redis.call('GET', KEYS[1])
            if current then return current else return 0 end
        end
        local total = redis.call('INCRBY', KEYS[1], amount)
        if total == amount then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return total";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisUsageBudgetService"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="configuration">Application configuration (reads <c>UsageBudget:DailyTokenBudget</c>).</param>
    /// <param name="logger">The logger.</param>
    public RedisUsageBudgetService(
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<RedisUsageBudgetService> logger)
    {
        _redis = redis;
        _logger = logger;
        _dailyTokenBudget = configuration.GetValue<long?>("UsageBudget:DailyTokenBudget") ?? DefaultDailyTokenBudget;
    }

    /// <inheritdoc/>
    public async Task<bool> IsDailyTokenBudgetExceededAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        if (_dailyTokenBudget <= 0)
        {
            return false; // Budget disabled.
        }

        var usage = await GetDailyTokenUsageAsync(userProfileId, cancellationToken);
        return usage >= _dailyTokenBudget;
    }

    /// <inheritdoc/>
    public async Task<long> RecordTokenUsageAsync(Guid userProfileId, long tokens, CancellationToken cancellationToken = default)
    {
        if (_dailyTokenBudget <= 0)
        {
            return 0; // Budget disabled.
        }

        if (tokens <= 0)
        {
            return await GetDailyTokenUsageAsync(userProfileId, cancellationToken);
        }

        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            IncrementWithExpiryScript,
            [GetKey(userProfileId)],
            [tokens, WindowMilliseconds]);

        var newTotal = (long)result;

        if (newTotal >= _dailyTokenBudget)
        {
            _logger.LogWarning(
                "User {UserProfileId} reached the daily token budget: {Total}/{Budget}",
                userProfileId,
                newTotal,
                _dailyTokenBudget);
        }

        return newTotal;
    }

    /// <inheritdoc/>
    public async Task<long> GetDailyTokenUsageAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(GetKey(userProfileId));
        return value.HasValue && value.TryParse(out long usage) ? usage : 0;
    }

    /// <inheritdoc/>
    public async Task<UsageBudgetSnapshot> GetDailyTokenUsageSnapshotAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        var usage = await GetDailyTokenUsageAsync(userProfileId, cancellationToken);
        if (_dailyTokenBudget <= 0)
        {
            return new UsageBudgetSnapshot
            {
                IsEnabled = false,
                UsedTokens = usage,
                DailyTokenBudget = 0,
                RemainingTokens = 0,
                UsedRatio = 0,
                IsExceeded = false
            };
        }

        return new UsageBudgetSnapshot
        {
            IsEnabled = true,
            UsedTokens = usage,
            DailyTokenBudget = _dailyTokenBudget,
            RemainingTokens = Math.Max(0, _dailyTokenBudget - usage),
            UsedRatio = Math.Clamp((double)usage / _dailyTokenBudget, 0, 1),
            IsExceeded = usage >= _dailyTokenBudget
        };
    }

    private static RedisKey GetKey(Guid userProfileId) => $"{KeyPrefix}{userProfileId}";
}
