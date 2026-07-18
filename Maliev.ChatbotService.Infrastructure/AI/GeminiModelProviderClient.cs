using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Model provider adapter for Google Gemini.
/// </summary>
public sealed class GeminiModelProviderClient : IModelProviderClient
{
    private readonly GeminiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiModelProviderClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="metrics">The conversation metrics.</param>
    /// <param name="logger">The Gemini client logger.</param>
    public GeminiModelProviderClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ConversationMetrics metrics,
        ILogger<GeminiClient> logger)
    {
        _client = new GeminiClient(httpClient, configuration, metrics, logger);
    }

    /// <inheritdoc/>
    public string ProviderName => "gemini";

    /// <inheritdoc/>
    public Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default) =>
        _client.SendMessageAsync(request, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(
        GeminiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var streamEvent in _client.StreamMessageAsync(request, cancellationToken))
        {
            yield return streamEvent;
        }
    }
}
