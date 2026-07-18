namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Provider-neutral client for multimodal chat model calls.
/// </summary>
public interface IModelProviderClient
{
    /// <summary>
    /// Gets the provider key used in configuration.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends a message to the configured model provider.
    /// </summary>
    /// <param name="request">The provider-neutral chat request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response from the model provider.</returns>
    Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a response from the configured model provider.
    /// </summary>
    /// <param name="request">The provider-neutral chat request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Incremental response events from the model provider.</returns>
    IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates model provider clients without instantiating unused providers.
/// </summary>
public interface IModelProviderClientFactory
{
    /// <summary>
    /// Creates a model provider client for the requested provider key.
    /// </summary>
    /// <param name="providerName">The provider key from configuration.</param>
    /// <returns>The matching provider client.</returns>
    IModelProviderClient Create(string? providerName);
}
