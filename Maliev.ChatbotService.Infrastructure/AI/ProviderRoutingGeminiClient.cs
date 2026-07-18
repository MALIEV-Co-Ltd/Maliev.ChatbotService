using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Compatibility facade that routes legacy Gemini-shaped requests to the configured model provider.
/// </summary>
public sealed class ProviderRoutingGeminiClient : IGeminiClient
{
    private readonly IModelProviderClientFactory _clientFactory;
    private readonly string _providerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderRoutingGeminiClient"/> class.
    /// </summary>
    /// <param name="clientFactory">The model provider client factory.</param>
    /// <param name="configuration">The configuration.</param>
    public ProviderRoutingGeminiClient(
        IModelProviderClientFactory clientFactory,
        IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _providerName = configuration["Llm:Provider"] ?? "gemini";
    }

    /// <inheritdoc/>
    public Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default) =>
        _clientFactory.Create(_providerName).SendMessageAsync(request, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(
        GeminiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var streamEvent in _clientFactory.Create(_providerName).StreamMessageAsync(request, cancellationToken))
        {
            yield return streamEvent;
        }
    }
}
