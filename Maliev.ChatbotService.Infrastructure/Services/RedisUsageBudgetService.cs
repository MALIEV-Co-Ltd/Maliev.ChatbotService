using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Redis-backed daily model budget (S2). Tracks cumulative model token and estimated cost usage per
/// user in rolling 24-hour windows and enforces soft ceilings. Each window is implemented exactly
/// like the per-hour message rate limiter: a counter whose TTL is set only when the key is first
/// created, so usage rolls off 24 hours after the first recorded charge of the window.
/// </summary>
public class RedisUsageBudgetService : IUsageBudgetService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisUsageBudgetService> _logger;
    private readonly long _dailyTokenBudget;
    private readonly long _dailyCostBudgetMicroUsd;
    private readonly long _googleSearchGroundingFreeDailyPromptAllowance;

    private const string TokenKeyPrefix = "chatbot:tokenbudget:";
    private const string CostKeyPrefix = "chatbot:costbudget:";
    private const string GoogleSearchGroundingPromptKeyPrefix = "chatbot:gemini:google-search-grounding:flash-shared:";

    // Default daily budget per user. Deliberately a high code default (not in appsettings.json) so the
    // integration suite — which records only ~100 mock tokens per message — never trips it without
    // explicitly overriding the setting (the 38-spurious-429 lesson from the per-IP limiter). Set the
    // configured value to 0 to disable the budget entirely.
    private const long DefaultDailyTokenBudget = 2_000_000L;
    private const long DefaultDailyCostBudgetMicroUsd = 5_000_000L;
    private const long DefaultGoogleSearchGroundingFreeDailyPromptAllowance = 1_500L;

    private static readonly long WindowMilliseconds = (long)TimeSpan.FromHours(24).TotalMilliseconds;
    private static readonly long DailyAllowanceKeyMilliseconds = (long)TimeSpan.FromDays(2).TotalMilliseconds;

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

    private const string IncrementWithPreviousAndExpiryScript = @"
        local amount = tonumber(ARGV[1])
        if amount <= 0 then
            local current = redis.call('GET', KEYS[1])
            if current then return {tonumber(current), tonumber(current)} else return {0, 0} end
        end
        local total = redis.call('INCRBY', KEYS[1], amount)
        if total == amount then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return {total - amount, total}";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisUsageBudgetService"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="configuration">Application configuration for token, cost, and grounding allowance budgets.</param>
    /// <param name="logger">The logger.</param>
    public RedisUsageBudgetService(
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<RedisUsageBudgetService> logger)
    {
        _redis = redis;
        _logger = logger;
        _dailyTokenBudget = configuration.GetValue<long?>("UsageBudget:DailyTokenBudget") ?? DefaultDailyTokenBudget;
        _dailyCostBudgetMicroUsd = configuration.GetValue<long?>("UsageBudget:DailyCostBudgetMicroUsd") ?? DefaultDailyCostBudgetMicroUsd;
        _googleSearchGroundingFreeDailyPromptAllowance = Math.Max(
            0,
            configuration.GetValue<long?>("UsageBudget:GoogleSearchGroundingFreeDailyPromptAllowance") ??
                DefaultGoogleSearchGroundingFreeDailyPromptAllowance);
    }

    /// <inheritdoc/>
    public async Task<bool> IsDailyTokenBudgetExceededAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetDailyTokenUsageSnapshotAsync(userProfileId, cancellationToken);
        return snapshot.IsExceeded;
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

        var result = await RecordModelUsageAsync(
            userProfileId,
            new UsageBudgetCharge { Tokens = tokens },
            cancellationToken);
        return result.UsedTokens;
    }

    /// <inheritdoc/>
    public async Task<UsageBudgetRecordResult> RecordModelUsageAsync(
        Guid userProfileId,
        UsageBudgetCharge usage,
        CancellationToken cancellationToken = default)
    {
        var tokenTotal = _dailyTokenBudget > 0
            ? await IncrementUsageAsync(GetTokenKey(userProfileId), usage.Tokens)
            : 0;
        var groundingCharge = _dailyCostBudgetMicroUsd > 0
            ? await CalculateGoogleSearchGroundingChargeAsync(usage)
            : GoogleSearchGroundingCharge.Empty;
        var costChargeMicroUsd = AddMicroUsd(usage.CostMicroUsd, groundingCharge.ChargedMicroUsd);
        var costTotal = _dailyCostBudgetMicroUsd > 0
            ? await IncrementUsageAsync(GetCostKey(userProfileId), costChargeMicroUsd)
            : 0;

        if (_dailyTokenBudget > 0 && tokenTotal >= _dailyTokenBudget)
        {
            _logger.LogWarning(
                "User {UserProfileId} reached the daily token budget: {Total}/{Budget}",
                userProfileId,
                tokenTotal,
                _dailyTokenBudget);
        }

        if (_dailyCostBudgetMicroUsd > 0 && costTotal >= _dailyCostBudgetMicroUsd)
        {
            _logger.LogWarning(
                "User {UserProfileId} reached the daily Gemini cost budget: {TotalMicroUsd}/{BudgetMicroUsd}",
                userProfileId,
                costTotal,
                _dailyCostBudgetMicroUsd);
        }

        return new UsageBudgetRecordResult
        {
            UsedTokens = tokenTotal,
            UsedCostMicroUsd = costTotal,
            FreeGoogleSearchGroundingPromptCount = groundingCharge.FreePromptCount,
            BillableGoogleSearchGroundingPromptCount = groundingCharge.BillablePromptCount,
            ChargedGoogleSearchGroundingMicroUsd = groundingCharge.ChargedMicroUsd
        };
    }

    /// <inheritdoc/>
    public async Task<long> GetDailyTokenUsageAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await GetUsageAsync(GetTokenKey(userProfileId));
    }

    /// <inheritdoc/>
    public async Task<UsageBudgetSnapshot> GetDailyTokenUsageSnapshotAsync(
        Guid userProfileId,
        CancellationToken cancellationToken = default)
    {
        var tokenUsage = await GetDailyTokenUsageAsync(userProfileId, cancellationToken);
        var costUsage = await GetUsageAsync(GetCostKey(userProfileId));
        var tokenBudgetEnabled = _dailyTokenBudget > 0;
        var costBudgetEnabled = _dailyCostBudgetMicroUsd > 0;
        var tokenExceeded = tokenBudgetEnabled && tokenUsage >= _dailyTokenBudget;
        var costExceeded = costBudgetEnabled && costUsage >= _dailyCostBudgetMicroUsd;

        return new UsageBudgetSnapshot
        {
            IsEnabled = tokenBudgetEnabled || costBudgetEnabled,
            UsedTokens = tokenUsage,
            DailyTokenBudget = tokenBudgetEnabled ? _dailyTokenBudget : 0,
            RemainingTokens = tokenBudgetEnabled ? Math.Max(0, _dailyTokenBudget - tokenUsage) : 0,
            UsedRatio = tokenBudgetEnabled ? Math.Clamp((double)tokenUsage / _dailyTokenBudget, 0, 1) : 0,
            UsedCostMicroUsd = costUsage,
            DailyCostBudgetMicroUsd = costBudgetEnabled ? _dailyCostBudgetMicroUsd : 0,
            RemainingCostMicroUsd = costBudgetEnabled ? Math.Max(0, _dailyCostBudgetMicroUsd - costUsage) : 0,
            CostUsedRatio = costBudgetEnabled ? Math.Clamp((double)costUsage / _dailyCostBudgetMicroUsd, 0, 1) : 0,
            IsTokenExceeded = tokenExceeded,
            IsCostExceeded = costExceeded,
            IsExceeded = tokenExceeded || costExceeded
        };
    }

    private async Task<long> IncrementUsageAsync(RedisKey key, long amount)
    {
        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            IncrementWithExpiryScript,
            [key],
            [amount, WindowMilliseconds]);
        return (long)result;
    }

    private async Task<GoogleSearchGroundingCharge> CalculateGoogleSearchGroundingChargeAsync(UsageBudgetCharge usage)
    {
        var promptCount = Math.Max(0, usage.GoogleSearchGroundingPromptCount);
        var groundingMicroUsd = Math.Max(0, usage.GoogleSearchGroundingMicroUsd);
        if (promptCount == 0 || groundingMicroUsd == 0)
        {
            return GoogleSearchGroundingCharge.Empty;
        }

        if (_googleSearchGroundingFreeDailyPromptAllowance <= 0)
        {
            return new GoogleSearchGroundingCharge(0, promptCount, groundingMicroUsd);
        }

        var promptUsage = await IncrementGoogleSearchGroundingPromptUsageAsync(promptCount);
        var freeBefore = Math.Min(_googleSearchGroundingFreeDailyPromptAllowance, promptUsage.PreviousPromptCount);
        var freeAfter = Math.Min(_googleSearchGroundingFreeDailyPromptAllowance, promptUsage.TotalPromptCount);
        var freePromptCount = (int)Math.Max(0, freeAfter - freeBefore);
        var billablePromptCount = Math.Max(0, promptCount - freePromptCount);
        var chargedMicroUsd = billablePromptCount == 0
            ? 0
            : (long)Math.Round(
                groundingMicroUsd * ((decimal)billablePromptCount / promptCount),
                MidpointRounding.AwayFromZero);

        return new GoogleSearchGroundingCharge(freePromptCount, billablePromptCount, chargedMicroUsd);
    }

    private async Task<GoogleSearchGroundingPromptUsage> IncrementGoogleSearchGroundingPromptUsageAsync(int promptCount)
    {
        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            IncrementWithPreviousAndExpiryScript,
            [GetGoogleSearchGroundingPromptKey(DateTimeOffset.UtcNow)],
            [promptCount, DailyAllowanceKeyMilliseconds]);
        var values = (RedisResult[])result!;
        return new GoogleSearchGroundingPromptUsage((long)values[0], (long)values[1]);
    }

    private async Task<long> GetUsageAsync(RedisKey key)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        return value.HasValue && value.TryParse(out long usage) ? usage : 0;
    }

    private static RedisKey GetTokenKey(Guid userProfileId) => $"{TokenKeyPrefix}{userProfileId}";

    private static RedisKey GetCostKey(Guid userProfileId) => $"{CostKeyPrefix}{userProfileId}";

    private static RedisKey GetGoogleSearchGroundingPromptKey(DateTimeOffset timestamp) =>
        $"{GoogleSearchGroundingPromptKeyPrefix}{timestamp.UtcDateTime:yyyyMMdd}";

    private static long AddMicroUsd(long baseMicroUsd, long additionalMicroUsd)
    {
        var boundedBase = Math.Max(0, baseMicroUsd);
        var boundedAdditional = Math.Max(0, additionalMicroUsd);
        return long.MaxValue - boundedBase < boundedAdditional
            ? long.MaxValue
            : boundedBase + boundedAdditional;
    }

    private readonly record struct GoogleSearchGroundingPromptUsage(long PreviousPromptCount, long TotalPromptCount);

    private readonly record struct GoogleSearchGroundingCharge(
        int FreePromptCount,
        int BillablePromptCount,
        long ChargedMicroUsd)
    {
        public static GoogleSearchGroundingCharge Empty { get; } = new(0, 0, 0);
    }
}
