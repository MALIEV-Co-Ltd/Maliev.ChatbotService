using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// No-op model file staging service used when provider-native file staging is unavailable.
/// </summary>
public sealed class NoOpModelFileStagingService : IModelFileStagingService
{
    /// <inheritdoc/>
    public Task<ModelFileReference?> StageFileAsync(
        ModelFileStagingRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ModelFileReference?>(null);
}
