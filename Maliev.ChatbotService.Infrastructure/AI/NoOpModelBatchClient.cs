using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Batch client used when the configured model provider does not support model batch jobs.
/// </summary>
public sealed class NoOpModelBatchClient : IModelBatchClient
{
    /// <inheritdoc/>
    public Task<ModelBatchJob> CreateInlineGenerateContentBatchAsync(
        ModelBatchRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured model provider does not support batch jobs.");

    /// <inheritdoc/>
    public Task<ModelBatchJob> GetBatchAsync(
        string batchName,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The configured model provider does not support batch jobs.");
}
