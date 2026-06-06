using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for managing system instructions with Redis caching and PostgreSQL fallback.
/// </summary>
public class SystemInstructionService : ISystemInstructionService
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ICacheService _cache;
    private readonly IConversationMetrics _metrics;
    private readonly ILogger<SystemInstructionService> _logger;
    private const string CoreCacheKeyPrefix = "chatbot:system_instruction:active:core:";
    private const string MergedCacheKeyPrefix = "chatbot:system_instruction:merged:v";
    private const string CacheVersionKey = "chatbot:system_instruction:version";
    private const int MaxPromptCharacters = 8000;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);
    private static DateTimeOffset _nextRedisCheck = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionService"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="cache">The standardized cache service.</param>
    /// <param name="metrics">The conversation metrics.</param>
    /// <param name="logger">The logger.</param>
    public SystemInstructionService(
        ISystemInstructionRepository repository,
        ICacheService cache,
        IConversationMetrics metrics,
        ILogger<SystemInstructionService> logger)
    {
        _repository = repository;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveInstructionAsync(CancellationToken cancellationToken = default)
    {
        return await GetActiveInstructionAsync(null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveInstructionAsync(string? coreTopicKey, CancellationToken cancellationToken = default)
    {
        var redisAvailable = true;
        var normalizedCoreTopicKey = NormalizeCoreTopicKey(coreTopicKey);
        var cacheKey = $"{CoreCacheKeyPrefix}{normalizedCoreTopicKey ?? "default"}";

        try
        {
            // Try to get from Redis cache first
            var cachedData = await _cache.GetAsync<SystemInstruction>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved active system instruction for {CoreTopicKey} from Redis cache", normalizedCoreTopicKey ?? "default");
                _metrics.RecordCacheEvent("Entity", true);
                return cachedData;
            }

            _logger.LogDebug("Cache miss for active system instruction {CoreTopicKey}, querying PostgreSQL", normalizedCoreTopicKey ?? "default");
            _metrics.RecordCacheEvent("Entity", false);
        }
        catch (Exception ex)
        {
            redisAvailable = false;
            _logger.LogWarning(ex, "Redis unavailable - falling back to direct PostgreSQL reads. Response times may be degraded.");
        }

        // Fallback to PostgreSQL (either cache miss or Redis unavailable)
        var instruction = await _repository.GetActiveCoreAsync(normalizedCoreTopicKey, cancellationToken);
        if (instruction is null && normalizedCoreTopicKey is not null)
        {
            instruction = await _repository.GetActiveCoreAsync(cancellationToken);
        }

        if (instruction != null && redisAvailable)
        {
            try
            {
                // Attempt to cache the result
                await _cache.SetAsync(cacheKey, instruction, _cacheExpiration, cancellationToken);
                _logger.LogDebug("Cached active system instruction {CoreTopicKey} in Redis", normalizedCoreTopicKey ?? "default");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache write failed - continuing with PostgreSQL-only mode");
            }
        }
        // If Redis is already known to be down, only check occasionally to avoid latency on every request
        if (!redisAvailable && DateTimeOffset.UtcNow < _nextRedisCheck)
        {
            _logger.LogDebug("Redis still unavailable, continuing with PostgreSQL-only reads (skipping recovery check until {NextCheck})", _nextRedisCheck);
            return instruction;
        }

        if (!redisAvailable)
        {
            // Try to detect Redis recovery
            try
            {
                var testKey = "chatbot:redis:health_check";
                await _cache.SetAsync(testKey, "test", TimeSpan.FromSeconds(10), cancellationToken);

                _logger.LogInformation("Redis connection recovered - caching resumed");

                // Cache the instruction now that Redis is back
                if (instruction != null)
                {
                    await _cache.SetAsync(cacheKey, instruction, _cacheExpiration, cancellationToken);
                }
            }
            catch
            {
                // Redis still unavailable - update backoff timer
                _nextRedisCheck = DateTimeOffset.UtcNow.AddMinutes(1);
                _logger.LogDebug("Redis still unavailable, continuing with PostgreSQL-only reads");
            }
        }

        return instruction;
    }

    /// <inheritdoc/>
    public async Task<string> GetMergedInstructionsAsync(IEnumerable<string> topicKeys, CancellationToken cancellationToken = default)
    {
        return await GetMergedInstructionsAsync(topicKeys, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GetMergedInstructionsAsync(IEnumerable<string> topicKeys, string? coreTopicKey, CancellationToken cancellationToken = default)
    {
        var topics = topicKeys.Distinct().OrderBy(t => t).ToList();
        var normalizedCoreTopicKey = NormalizeCoreTopicKey(coreTopicKey) ?? "default";

        // 0. Get current cache version to allow atomic invalidation of all merged prompts
        var version = await _cache.GetAsync<string>(CacheVersionKey, cancellationToken) ?? "1";
        var mergedCacheKey = $"{MergedCacheKeyPrefix}{version}:{normalizedCoreTopicKey}:{string.Join(",", topics)}";

        try
        {
            var cachedMerged = await _cache.GetAsync<string>(mergedCacheKey, cancellationToken);
            if (cachedMerged != null)
            {
                _logger.LogDebug("Retrieved merged system instructions from Redis cache");
                _metrics.RecordCacheEvent("Merged", true);
                return cachedMerged;
            }
            _metrics.RecordCacheEvent("Merged", false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable during merged instruction retrieval");
        }

        // 1. Get Core Instruction
        var core = await GetActiveInstructionAsync(coreTopicKey, cancellationToken);
        var promptParts = new List<string>();

        if (core != null)
        {
            promptParts.Add($"## CORE PERSONA AND SAFETY RULES\n{core.PersonaDefinition}\n\n{core.BusinessConstraints}");
        }
        else
        {
            promptParts.Add(GetDefaultSystemInstruction(normalizedCoreTopicKey));
        }

        // 2. Get Topic Instructions
        if (topics.Any())
        {
            var topicInstructions = await _repository.GetActiveByTopicsAsync(topics, cancellationToken);
            if (topicInstructions.Any())
            {
                var topicHeaderAdded = false;
                var omittedTopicKeys = new List<string>();
                var currentTotalLength = promptParts.Sum(p => p.Length);

                foreach (var topic in topicInstructions)
                {
                    var topicKey = topic.TopicKey ?? string.Empty;
                    var topicText = $"### Topic: {topicKey}\n{topic.PersonaDefinition}\n\n{topic.BusinessConstraints}";

                    if (currentTotalLength + topicText.Length > MaxPromptCharacters)
                    {
                        omittedTopicKeys.Add(topicKey);
                        continue;
                    }

                    if (!topicHeaderAdded)
                    {
                        var header = "\n## SPECIALIZED DOMAIN KNOWLEDGE";
                        promptParts.Add(header);
                        currentTotalLength += header.Length;
                        topicHeaderAdded = true;
                    }

                    promptParts.Add(topicText);
                    currentTotalLength += topicText.Length;
                }

                if (omittedTopicKeys.Any())
                {
                    var topicsSummary = string.Join(", ", omittedTopicKeys);
                    _logger.LogWarning(
                        "System instruction context limit reached. Omitted {OmittedTopicCount} topic instruction(s): {OmittedTopicKeys}.",
                        omittedTopicKeys.Count,
                        topicsSummary);
                }
            }
        }

        var mergedPrompt = string.Join("\n\n", promptParts);

        try
        {
            await _cache.SetAsync(mergedCacheKey, mergedPrompt, _cacheExpiration, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache merged system instructions");
        }

        return mergedPrompt;
    }

    private static string GetDefaultSystemInstruction(string? coreTopicKey)
    {
        if (string.Equals(coreTopicKey, "website", StringComparison.OrdinalIgnoreCase))
        {
            return """
                You are Mali (น้องมะลิ), MALIEV's customer-facing manufacturing assistant for www.maliev.com.
                Help customers with MALIEV services, materials, quote preparation, order guidance, delivery, and support.
                Stay within MALIEV manufacturing topics and politely redirect unrelated requests.
                """;
        }

        return """
            You are Mali (มะลิ), a bilingual (Thai/English) AI operations assistant for Maliev Manufacturing Company.
            You help internal staff with CRM, sales, finance, HR, inventory, and analytics.
            Be professional, warm, concise, and action-oriented. Match the user's language preference.
            """;
    }

    /// <inheritdoc/>
    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveByPatternAsync($"{CoreCacheKeyPrefix}*", cancellationToken);

            // Increment version to invalidate all merged prompt combinations
            var versionString = await _cache.GetAsync<string>(CacheVersionKey, cancellationToken) ?? "1";
            if (int.TryParse(versionString, out var version))
            {
                await _cache.SetAsync(CacheVersionKey, (version + 1).ToString(), TimeSpan.FromDays(7), cancellationToken);
            }

            _logger.LogInformation("Invalidated system instruction cache and incremented version");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache");
        }
    }

    private static string? NormalizeCoreTopicKey(string? coreTopicKey)
    {
        return string.IsNullOrWhiteSpace(coreTopicKey)
            ? null
            : coreTopicKey.Trim().ToLowerInvariant();
    }
}
