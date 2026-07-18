using System.Text.Json;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Redis-backed durable buffer for debounced webhook messages (S6). All state lives in Redis so a
/// process crash cannot drop in-flight work — a claimed session is leased (its due time pushed
/// forward), not removed, and the buffer is only trimmed on a successful acknowledgement. If the
/// worker dies, the lease expires and the next poll reclaims the session (at-least-once).
/// </summary>
public class RedisWebhookBufferQueue : IWebhookBufferQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisWebhookBufferQueue> _logger;

    private readonly TimeSpan _debounce;
    private readonly TimeSpan _lease;
    private readonly TimeSpan _retryBackoff;
    private readonly TimeSpan _contextTtl;
    private readonly int _maxAttempts;

    private const string BufferKeyPrefix = "chatbot:buffer:";
    private const string ContextKeyPrefix = "chatbot:webhook:ctx:";
    private const string AttemptsKeyPrefix = "chatbot:webhook:attempts:";
    private const string DueKey = "chatbot:webhook:due";

    // Claims due members and leases each forward (visibility timeout) in one atomic step so two polls
    // cannot process the same session concurrently. KEYS[1]=due zset; ARGV[1]=asOf ms; ARGV[2]=leaseUntil ms.
    private const string ClaimScript = @"
        local due = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
        for _, member in ipairs(due) do
            redis.call('ZADD', KEYS[1], ARGV[2], member)
        end
        return due";

    // Removes exactly the processed messages from the head of the buffer (messages appended during
    // processing survive). Clears the schedule when drained, else reschedules the remainder.
    // KEYS: 1=buffer 2=due 3=ctx 4=attempts; ARGV: 1=count 2=member 3=rescheduleScore ms.
    private const string AcknowledgeScript = @"
        local count = tonumber(ARGV[1])
        for i = 1, count do redis.call('LPOP', KEYS[1]) end
        local remaining = redis.call('LLEN', KEYS[1])
        redis.call('DEL', KEYS[4])
        if remaining == 0 then
            redis.call('ZREM', KEYS[2], ARGV[2])
            redis.call('DEL', KEYS[3])
        else
            redis.call('ZADD', KEYS[2], ARGV[3], ARGV[2])
        end
        return remaining";

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisWebhookBufferQueue"/> class.
    /// </summary>
    public RedisWebhookBufferQueue(
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<RedisWebhookBufferQueue> logger)
    {
        _redis = redis;
        _logger = logger;

        _debounce = TimeSpan.FromSeconds(configuration.GetValue<double?>("Webhook:DebounceSeconds") ?? 2);
        // Lease must exceed the worst-case agent turn so a slow (not crashed) worker is not reclaimed
        // mid-flight: SendMessageCommandHandler holds its per-session lock for ~330s (C2).
        _lease = TimeSpan.FromSeconds(configuration.GetValue<double?>("Webhook:LeaseSeconds") ?? 360);
        _retryBackoff = TimeSpan.FromSeconds(configuration.GetValue<double?>("Webhook:RetryBackoffSeconds") ?? 15);
        _maxAttempts = configuration.GetValue<int?>("Webhook:MaxProcessingAttempts") ?? 3;
        // Reply context must outlive every possible retry so a late attempt can still reply.
        _contextTtl = (_lease * _maxAttempts) + _debounce + TimeSpan.FromMinutes(1);
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(Guid sessionId, ProcessWebhookCommand command, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var member = sessionId.ToString();

        await db.ListRightPushAsync(BufferKey(sessionId), command.MessageText);

        var context = new WebhookContext
        {
            Channel = (int)command.Channel,
            ReplyToken = command.ReplyToken,
            RecipientId = command.RecipientId,
            PlatformUserId = command.PlatformUserId
        };
        await db.StringSetAsync(ContextKey(sessionId), JsonSerializer.Serialize(context), _contextTtl);

        // Schedule (or extend the debounce of) this session. GreaterThan adds a brand-new member and
        // still extends the debounce (time only moves forward), but must NOT pull the score backward
        // when the session is currently leased for processing — otherwise a message arriving mid-turn
        // would re-make the session due, get it re-claimed alongside the in-flight worker, and deliver a
        // duplicate reply. The remainder is instead picked up by the worker's AcknowledgeAsync reschedule.
        var dueMs = DateTimeOffset.UtcNow.Add(_debounce).ToUnixTimeMilliseconds();
        await db.SortedSetAddAsync(DueKey, member, dueMs, SortedSetWhen.GreaterThan);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> ClaimDueSessionsAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var asOfMs = asOf.ToUnixTimeMilliseconds();
        var leaseUntilMs = asOf.Add(_lease).ToUnixTimeMilliseconds();

        var result = await db.ScriptEvaluateAsync(ClaimScript, [DueKey], [asOfMs, leaseUntilMs]);
        if (result.IsNull)
        {
            return [];
        }

        var members = (RedisValue[])result!;
        var claimed = new List<Guid>(members.Length);

        foreach (var member in members)
        {
            if (!Guid.TryParse(member.ToString(), out var sessionId))
            {
                continue;
            }

            // Count each lease as an attempt; this bounds both crash-loops (the worker died and the
            // lease expired) and transient-failure-loops with one cap.
            var attemptsKey = AttemptsKey(sessionId);
            var attempts = await db.StringIncrementAsync(attemptsKey);
            await db.KeyExpireAsync(attemptsKey, _contextTtl);

            if (attempts > _maxAttempts)
            {
                _logger.LogError(
                    "Dropping webhook buffer for session {SessionId} after {Attempts} processing attempts",
                    sessionId, attempts - 1);
                await db.SortedSetRemoveAsync(DueKey, member);
                await db.KeyDeleteAsync(BufferKey(sessionId));
                await db.KeyDeleteAsync(ContextKey(sessionId));
                await db.KeyDeleteAsync(attemptsKey);
                continue;
            }

            claimed.Add(sessionId);
        }

        return claimed;
    }

    /// <inheritdoc/>
    public async Task<BufferedWebhookBatch?> PeekAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();

        var messages = await db.ListRangeAsync(BufferKey(sessionId), 0, -1);
        if (messages.Length == 0)
        {
            return null;
        }

        var contextJson = await db.StringGetAsync(ContextKey(sessionId));
        if (!contextJson.HasValue)
        {
            // Buffer present but reply context gone (TTL is sized to prevent this). Cannot route a
            // reply; leave the buffer for the attempt cap to drop on a later claim.
            _logger.LogWarning("Webhook buffer for session {SessionId} has no reply context; cannot process", sessionId);
            return null;
        }

        WebhookContext? context;
        try
        {
            context = JsonSerializer.Deserialize<WebhookContext>(contextJson.ToString());
        }
        catch (JsonException)
        {
            context = null;
        }

        if (context is null)
        {
            _logger.LogWarning("Webhook reply context for session {SessionId} is unreadable; cannot process", sessionId);
            return null;
        }

        var combined = string.Join("\n", messages.Select(m => m.ToString()));
        return new BufferedWebhookBatch(
            sessionId,
            combined,
            messages.Length,
            (Channel)context.Channel,
            context.ReplyToken,
            context.RecipientId,
            context.PlatformUserId ?? string.Empty);
    }

    /// <inheritdoc/>
    public async Task AcknowledgeAsync(Guid sessionId, int processedCount, CancellationToken cancellationToken = default)
    {
        if (processedCount <= 0)
        {
            return;
        }

        var db = _redis.GetDatabase();
        var rescheduleMs = DateTimeOffset.UtcNow.Add(_debounce).ToUnixTimeMilliseconds();

        await db.ScriptEvaluateAsync(
            AcknowledgeScript,
            [BufferKey(sessionId), DueKey, ContextKey(sessionId), AttemptsKey(sessionId)],
            [processedCount, sessionId.ToString(), rescheduleMs]);
    }

    /// <inheritdoc/>
    public async Task ReportFailureAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Keep the buffer; bring the session due again after a short backoff (the attempt cap enforced
        // at claim time ultimately drops a poison batch).
        var db = _redis.GetDatabase();
        var retryMs = DateTimeOffset.UtcNow.Add(_retryBackoff).ToUnixTimeMilliseconds();
        await db.SortedSetAddAsync(DueKey, sessionId.ToString(), retryMs);
    }

    private static RedisKey BufferKey(Guid sessionId) => $"{BufferKeyPrefix}{sessionId}";
    private static RedisKey ContextKey(Guid sessionId) => $"{ContextKeyPrefix}{sessionId}";
    private static RedisKey AttemptsKey(Guid sessionId) => $"{AttemptsKeyPrefix}{sessionId}";

    private sealed class WebhookContext
    {
        public int Channel { get; set; }
        public string? ReplyToken { get; set; }
        public string? RecipientId { get; set; }
        public string? PlatformUserId { get; set; }
    }
}
