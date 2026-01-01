using System.Security.Cryptography;
using System.Text;
using Line.Messaging;
using Line.Messaging.Webhooks;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.ChatbotService.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of LINE messaging platform client.
/// </summary>
public class LineClient : ILineClient
{
    private readonly LineMessagingClient _messagingClient;
    private readonly string _channelSecret;

    /// <summary>
    /// Initializes a new instance of the <see cref="LineClient"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public LineClient(IConfiguration configuration)
    {
        var channelAccessToken = configuration["LINE:ChannelAccessToken"]
            ?? throw new InvalidOperationException("LINE ChannelAccessToken is not configured");
        _channelSecret = configuration["LINE:ChannelSecret"]
            ?? throw new InvalidOperationException("LINE ChannelSecret is not configured");

        _messagingClient = new LineMessagingClient(channelAccessToken);
    }

    /// <summary>
    /// Sends a text message to a LINE user.
    /// </summary>
    /// <param name="replyToken">The reply token from the webhook event.</param>
    /// <param name="messageText">The message text to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendTextMessageAsync(string replyToken, string messageText, CancellationToken cancellationToken = default)
    {
        var messages = new List<ISendMessage>
        {
            new TextMessage(messageText)
        };

        await _messagingClient.ReplyMessageAsync(replyToken, messages);
        await Task.CompletedTask; // Satisfy compiler for cancellationToken parameter
    }

    /// <summary>
    /// Sends a Flex Message to a LINE user.
    /// </summary>
    /// <param name="replyToken">The reply token from the webhook event.</param>
    /// <param name="flexMessageJson">The Flex Message JSON structure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendFlexMessageAsync(string replyToken, string flexMessageJson, CancellationToken cancellationToken = default)
    {
        var flexContainer = FlexMessage.CreateBubbleMessage("Response")
            .SetBubbleContainer(Newtonsoft.Json.JsonConvert.DeserializeObject<BubbleContainer>(flexMessageJson)
                ?? throw new ArgumentException("Invalid Flex Message JSON", nameof(flexMessageJson)));

        var messages = new List<ISendMessage> { flexContainer };

        await _messagingClient.ReplyMessageAsync(replyToken, messages);
        await Task.CompletedTask; // Satisfy compiler for cancellationToken parameter
    }

    /// <summary>
    /// Verifies the signature of a LINE webhook request.
    /// </summary>
    /// <param name="signature">The X-Line-Signature header value.</param>
    /// <param name="requestBody">The raw request body.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    public bool VerifySignature(string signature, string requestBody)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(requestBody))
        {
            return false;
        }

        try
        {
            var key = Encoding.UTF8.GetBytes(_channelSecret);
            var body = Encoding.UTF8.GetBytes(requestBody);

            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(body);
            var computedSignature = Convert.ToBase64String(hash);

            return signature.Equals(computedSignature, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
