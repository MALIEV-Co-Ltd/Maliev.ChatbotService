namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Client interface for LINE messaging platform integration.
/// </summary>
public interface ILineClient
{
    /// <summary>
    /// Sends a text message to a LINE user.
    /// </summary>
    /// <param name="replyToken">The reply token from the webhook event.</param>
    /// <param name="messageText">The message text to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendTextMessageAsync(string replyToken, string messageText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a Flex Message to a LINE user.
    /// </summary>
    /// <param name="replyToken">The reply token from the webhook event.</param>
    /// <param name="flexMessageJson">The Flex Message JSON structure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendFlexMessageAsync(string replyToken, string flexMessageJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the signature of a LINE webhook request.
    /// </summary>
    /// <param name="signature">The X-Line-Signature header value.</param>
    /// <param name="requestBody">The raw request body.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    bool VerifySignature(string signature, string requestBody);
}
