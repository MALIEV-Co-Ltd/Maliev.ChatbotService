using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Model context cache service used when provider-side context caching is unavailable.
/// </summary>
public sealed class NoOpModelContextCacheService : IModelContextCacheService
{
    /// <inheritdoc/>
    public Task<ModelContextCacheReference?> GetOrCreateSystemInstructionCacheAsync(
        ModelContextCacheRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ModelContextCacheReference?>(null);
}
