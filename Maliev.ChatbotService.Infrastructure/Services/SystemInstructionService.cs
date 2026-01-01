using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for managing system instructions with Redis caching and PostgreSQL fallback.
/// </summary>
public class SystemInstructionService : ISystemInstructionService
{
    private readonly ISystemInstructionRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SystemInstructionService> _logger;
    private const string CacheKey = "chatbot:system_instruction:active";
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionService"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="logger">The logger.</param>
    public SystemInstructionService(
        ISystemInstructionRepository repository,
        IDistributedCache cache,
        ILogger<SystemInstructionService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveInstructionAsync(CancellationToken cancellationToken = default)
    {
        var redisAvailable = true;

        try
        {
            // Try to get from Redis cache first
            var cachedData = await _cache.GetStringAsync(CacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("Retrieved active system instruction from Redis cache");
                return JsonSerializer.Deserialize<SystemInstruction>(cachedData);
            }

            _logger.LogDebug("Cache miss for active system instruction, querying PostgreSQL");
        }
        catch (Exception ex)
        {
            redisAvailable = false;
            _logger.LogWarning(ex, "Redis unavailable - falling back to direct PostgreSQL reads. Response times may be degraded.");
        }

        // Fallback to PostgreSQL (either cache miss or Redis unavailable)
        var instruction = await _repository.GetActiveAsync(cancellationToken);

        if (instruction != null && redisAvailable)
        {
            try
            {
                // Attempt to cache the result
                var serialized = JsonSerializer.Serialize(instruction);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheExpiration
                };
                await _cache.SetStringAsync(CacheKey, serialized, options, cancellationToken);
                _logger.LogDebug("Cached active system instruction in Redis");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache write failed - continuing with PostgreSQL-only mode");
            }
        }
        else if (instruction != null && !redisAvailable)
        {
            // Try to detect Redis recovery
            try
            {
                var testKey = "chatbot:redis:health_check";
                await _cache.SetStringAsync(testKey, "test", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                }, cancellationToken);

                _logger.LogInformation("Redis connection recovered - caching resumed");

                // Cache the instruction now that Redis is back
                var serialized = JsonSerializer.Serialize(instruction);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheExpiration
                };
                await _cache.SetStringAsync(CacheKey, serialized, options, cancellationToken);
            }
            catch
            {
                // Redis still unavailable - continue with PostgreSQL only
                _logger.LogDebug("Redis still unavailable, continuing with PostgreSQL-only reads");
            }
        }

        return instruction;
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
