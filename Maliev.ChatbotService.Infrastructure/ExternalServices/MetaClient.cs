using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of Meta platforms (Facebook, Instagram, WhatsApp) client.
/// </summary>
public class MetaClient : IMetaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _appSecret;
    private readonly ILogger<MetaClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetaClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger.</param>
    public MetaClient(HttpClient httpClient, IConfiguration configuration, ILogger<MetaClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _appSecret = configuration["Meta:AppSecret"]
            ?? throw new InvalidOperationException("Meta AppSecret is not configured");

        var accessToken = configuration["Meta:PageAccessToken"]
            ?? throw new InvalidOperationException("Meta PageAccessToken is not configured");

        _httpClient.BaseAddress = new Uri("https://graph.facebook.com/v21.0/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
    }

    /// <summary>
    /// Sends a text message to a Meta platform user.
    /// </summary>
    /// <param name="recipientId">The recipient's platform-specific ID.</param>
    /// <param name="messageText">The message text to send.</param>
    /// <param name="platform">The Meta platform (Facebook, Instagram, WhatsApp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendTextMessageAsync(string recipientId, string messageText, string platform, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            recipient = new { id = recipientId },
            message = new { text = messageText },
            messaging_type = "RESPONSE"
        };

        var endpoint = GetMessagingEndpoint(platform);
        var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to send Meta text message to {Platform}. Status: {Status}, Error: {Error}",
                platform, response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Sends a generic template message to a Meta platform user.
    /// </summary>
    /// <param name="recipientId">The recipient's platform-specific ID.</param>
    /// <param name="templatePayload">The template payload JSON.</param>
    /// <param name="platform">The Meta platform (Facebook, Instagram, WhatsApp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendGenericTemplateAsync(string recipientId, string templatePayload, string platform, CancellationToken cancellationToken = default)
    {
        var template = JsonSerializer.Deserialize<JsonElement>(templatePayload);

        var payload = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                attachment = new
                {
                    type = "template",
                    payload = template
                }
            },
            messaging_type = "RESPONSE"
        };

        var endpoint = GetMessagingEndpoint(platform);
        var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to send Meta generic template to {Platform}. Status: {Status}, Error: {Error}",
                platform, response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Verifies the signature of a Meta webhook request.
    /// </summary>
    /// <param name="signature">The X-Hub-Signature-256 header value.</param>
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
            // Signature format: "sha256=<hash>"
            if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var expectedHash = signature["sha256=".Length..];
            var key = Encoding.UTF8.GetBytes(_appSecret);
            var body = Encoding.UTF8.GetBytes(requestBody);

            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(body);
            var computedHash = Convert.ToHexString(hash).ToLowerInvariant();

            return expectedHash.Equals(computedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the messaging endpoint for the specified platform.
    /// </summary>
    /// <param name="platform">The Meta platform.</param>
    /// <returns>The messaging endpoint path.</returns>
    private static string GetMessagingEndpoint(string platform)
    {
        return platform.ToLowerInvariant() switch
        {
            "facebook" => "me/messages",
            "instagram" => "me/messages",
            "whatsapp" => "me/messages", // Note: WhatsApp uses a different API structure, simplified here
            _ => throw new ArgumentException($"Unsupported Meta platform: {platform}", nameof(platform))
        };
    }
}
