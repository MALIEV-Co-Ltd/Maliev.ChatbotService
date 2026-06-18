namespace Maliev.ChatbotService.Api.RateLimiting;

/// <summary>
/// Named rate-limiter policies for the Chatbot API.
/// </summary>
public static class ChatbotRateLimiterPolicies
{
    /// <summary>
    /// Per-client-IP limit on message sends. Independent of <c>UserProfileId</c> so an anonymous
    /// caller cannot reset their budget by initiating a fresh session (S1).
    /// </summary>
    public const string MessagesPerIp = "messages-per-ip";
}
