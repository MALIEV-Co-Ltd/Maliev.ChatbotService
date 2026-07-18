namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Provider-neutral client for asynchronous model batch jobs.
/// </summary>
public interface IModelBatchClient
{
    /// <summary>
    /// Creates an inline generateContent batch job.
    /// </summary>
    /// <param name="request">The batch request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created batch job state.</returns>
    Task<ModelBatchJob> CreateInlineGenerateContentBatchAsync(
        ModelBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest batch job state.
    /// </summary>
    /// <param name="batchName">The batch resource name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch job state.</returns>
    Task<ModelBatchJob> GetBatchAsync(string batchName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Inline model batch request.
/// </summary>
public sealed class ModelBatchRequest
{
    /// <summary>
    /// Gets or sets the model name. When null, the provider default model is used.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Gets or sets the batch display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional batch priority.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets the inline generateContent requests.
    /// </summary>
    public List<ModelBatchGenerateContentRequest> Requests { get; set; } = [];
}

/// <summary>
/// Single inline generateContent request with caller metadata.
/// </summary>
public sealed class ModelBatchGenerateContentRequest
{
    /// <summary>
    /// Gets or sets the model request.
    /// </summary>
    public GeminiRequest Request { get; set; } = new();

    /// <summary>
    /// Gets or sets metadata associated with this request.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

/// <summary>
/// Batch job state returned by the model provider.
/// </summary>
public sealed class ModelBatchJob
{
    /// <summary>
    /// Gets or sets the batch resource name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the provider reports the batch operation as complete.
    /// </summary>
    public bool Done { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific job state.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets inline responses returned for an inline batch.
    /// </summary>
    public List<ModelBatchInlineResponse> InlineResponses { get; set; } = [];
}

/// <summary>
/// Single inline batch response.
/// </summary>
public sealed class ModelBatchInlineResponse
{
    /// <summary>
    /// Gets or sets the request metadata.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets the generated response, if the request succeeded.
    /// </summary>
    public GeminiResponse? Response { get; set; }

    /// <summary>
    /// Gets or sets the provider error message, if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
