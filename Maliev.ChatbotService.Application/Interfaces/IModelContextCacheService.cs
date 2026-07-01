namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Resolves reusable model context cache references for stable prompt prefixes.
/// </summary>
public interface IModelContextCacheService
{
    /// <summary>
    /// Gets or creates a provider cache for a stable system instruction.
    /// </summary>
    /// <param name="request">The cache request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cache reference, or null when caching is unavailable or not beneficial.</returns>
    Task<ModelContextCacheReference?> GetOrCreateSystemInstructionCacheAsync(
        ModelContextCacheRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for caching a stable system instruction prefix.
/// </summary>
public sealed class ModelContextCacheRequest
{
    /// <summary>
    /// Gets or sets the model name for the cache. When null, the provider default model is used.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Gets or sets the stable system instruction text to cache.
    /// </summary>
    public string SystemInstruction { get; set; } = string.Empty;
}

/// <summary>
/// Reference to a provider-managed cached context resource.
/// </summary>
public sealed class ModelContextCacheReference
{
    /// <summary>
    /// Gets or sets the provider cache resource name.
    /// </summary>
    public string CachedContentName { get; set; } = string.Empty;
}
