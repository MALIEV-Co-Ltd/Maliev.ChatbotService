using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.Aspire.ServiceDefaults.Caching;
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
    private const string CacheKey = "chatbot:system_instruction:active";
    private const string MergedCacheKeyPrefix = "chatbot:system_instruction:merged:";
    private const int MaxPromptCharacters = 8000; // Character-based proxy for token limit
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
        var redisAvailable = true;

        try
        {
            // Try to get from Redis cache first
            var cachedData = await _cache.GetAsync<SystemInstruction>(CacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved active system instruction from Redis cache");
                _metrics.RecordCacheEvent("Entity", true);
                return cachedData;
            }

            _logger.LogDebug("Cache miss for active system instruction, querying PostgreSQL");
            _metrics.RecordCacheEvent("Entity", false);
        }
        catch (Exception ex)
        {
            redisAvailable = false;
            _logger.LogWarning(ex, "Redis unavailable - falling back to direct PostgreSQL reads. Response times may be degraded.");
        }

        // Fallback to PostgreSQL (either cache miss or Redis unavailable)
        var instruction = await _repository.GetActiveAsync(SystemInstructionCategory.Core, cancellationToken);

        if (instruction != null && redisAvailable)
        {
            try
            {
                // Attempt to cache the result
                await _cache.SetAsync(CacheKey, instruction, _cacheExpiration, cancellationToken);
                _logger.LogDebug("Cached active system instruction in Redis");
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
                    await _cache.SetAsync(CacheKey, instruction, _cacheExpiration, cancellationToken);
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
        var topics = topicKeys.Distinct().OrderBy(t => t).ToList();
        var mergedCacheKey = $"{MergedCacheKeyPrefix}{string.Join(",", topics)}";

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
        var core = await GetActiveInstructionAsync(cancellationToken);
        var promptParts = new List<string>();

        if (core != null)
        {
            promptParts.Add($"## CORE PERSONA AND SAFETY RULES\n{core.PersonaDefinition}\n\n{core.BusinessConstraints}");
        }
        else
        {
            promptParts.Add(GetDefaultSystemInstruction());
        }

        // 2. Get Topic Instructions
        if (topics.Any())
        {
            var topicInstructions = await _repository.GetActiveByTopicsAsync(topics, cancellationToken);
            if (topicInstructions.Any())
            {
                var topicHeaderAdded = false;
                var currentTotalLength = promptParts.Sum(p => p.Length);

                foreach (var topic in topicInstructions)
                {
                    var topicText = $"### Topic: {topic.TopicKey}\n{topic.PersonaDefinition}\n\n{topic.BusinessConstraints}";

                    if (currentTotalLength + topicText.Length > MaxPromptCharacters)
                    {
                        _logger.LogWarning("System instruction truncation: Topic {TopicKey} omitted due to character limit", topic.TopicKey);
                        continue;
                    }

                    if (!topicHeaderAdded)
                    {
                        promptParts.Add("\n## SPECIALIZED DOMAIN KNOWLEDGE");
                        topicHeaderAdded = true;
                    }

                    promptParts.Add(topicText);
                    currentTotalLength += topicText.Length;
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

    private static string GetDefaultSystemInstruction()
    {
        return @"You are Mali, a helpful and knowledgeable AI assistant for Maliev Manufacturing Company. 
You specialize in manufacturing processes, materials, and customer inquiries about our services.
Professional, warm, and courteous in your communication style.";
    }

    /// <inheritdoc/>
    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(CacheKey, cancellationToken);
            _logger.LogInformation("Invalidated system instruction cache");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache");
        }
    }
}
