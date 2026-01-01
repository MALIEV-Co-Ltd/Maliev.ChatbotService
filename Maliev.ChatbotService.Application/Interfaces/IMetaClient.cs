namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Client interface for Meta platforms (Facebook, Instagram, WhatsApp) integration.
/// </summary>
public interface IMetaClient
{
    /// <summary>
    /// Sends a text message to a Meta platform user.
    /// </summary>
    /// <param name="recipientId">The recipient's platform-specific ID.</param>
    /// <param name="messageText">The message text to send.</param>
    /// <param name="platform">The Meta platform (Facebook, Instagram, WhatsApp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendTextMessageAsync(string recipientId, string messageText, string platform, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a generic template message to a Meta platform user.
    /// </summary>
    /// <param name="recipientId">The recipient's platform-specific ID.</param>
    /// <param name="templatePayload">The template payload JSON.</param>
    /// <param name="platform">The Meta platform (Facebook, Instagram, WhatsApp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendGenericTemplateAsync(string recipientId, string templatePayload, string platform, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the signature of a Meta webhook request.
    /// </summary>
    /// <param name="signature">The X-Hub-Signature-256 header value.</param>
    /// <param name="requestBody">The raw request body.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    bool VerifySignature(string signature, string requestBody);
}
