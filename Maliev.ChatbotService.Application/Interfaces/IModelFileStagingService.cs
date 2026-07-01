namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Stages accepted model input files into a provider-native temporary file store before generation.
/// </summary>
public interface IModelFileStagingService
{
    /// <summary>
    /// Uploads a file for later model prompting, or returns null when staging is unavailable.
    /// </summary>
    /// <param name="request">The file staging request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ModelFileReference?> StageFileAsync(
        ModelFileStagingRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for staging a model input file.
/// </summary>
public sealed class ModelFileStagingRequest
{
    /// <summary>Gets or sets the display file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the MIME type.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw file bytes.</summary>
    public byte[] Content { get; set; } = [];
}

/// <summary>
/// Provider-native staged file reference for model prompting.
/// </summary>
public sealed class ModelFileReference
{
    /// <summary>Gets or sets the provider file resource name, when available.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the provider file URI to use in model fileData parts.</summary>
    public string FileUri { get; set; } = string.Empty;

    /// <summary>Gets or sets the MIME type to send with the file URI.</summary>
    public string MimeType { get; set; } = string.Empty;
}
