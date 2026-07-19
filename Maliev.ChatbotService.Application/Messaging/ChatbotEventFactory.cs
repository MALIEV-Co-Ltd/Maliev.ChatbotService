using Maliev.MessagingContracts.Contracts.Chatbot;
using Maliev.MessagingContracts.Contracts.Shared;

namespace Maliev.ChatbotService.Application.Messaging;

/// <summary>
/// Creates schema-generated chatbot integration events with a consistent envelope.
/// </summary>
public static class ChatbotEventFactory
{
    private const string Publisher = "ChatbotService";
    private const string Version = "1.0";
    private static readonly IReadOnlyList<string> Consumers = Array.Empty<string>();

    /// <summary>
    /// Creates a session-created integration event.
    /// </summary>
    public static ChatbotSessionCreatedEvent SessionCreated(
        Guid sessionId,
        Guid userProfileId,
        string channel,
        string language,
        DateTimeOffset startTime,
        DateTimeOffset expiresAt)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        return new ChatbotSessionCreatedEvent(
            Guid.NewGuid(),
            nameof(ChatbotSessionCreatedEvent),
            MessageType.Event,
            Version,
            Publisher,
            Consumers,
            sessionId,
            null,
            occurredAt,
            true,
            new ChatbotSessionCreatedEventPayload(
                sessionId,
                userProfileId,
                channel,
                language,
                startTime,
                expiresAt));
    }

    /// <summary>
    /// Creates a session-closed integration event.
    /// </summary>
    public static ChatbotSessionClosedEvent SessionClosed(
        Guid sessionId,
        Guid userProfileId,
        string channel,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int totalMessageCount,
        string closureReason)
    {
        return new ChatbotSessionClosedEvent(
            Guid.NewGuid(),
            nameof(ChatbotSessionClosedEvent),
            MessageType.Event,
            Version,
            Publisher,
            Consumers,
            sessionId,
            null,
            endTime,
            true,
            new ChatbotSessionClosedEventPayload(
                sessionId,
                userProfileId,
                channel,
                startTime,
                endTime,
                totalMessageCount,
                closureReason));
    }

    /// <summary>
    /// Creates a message-received integration event.
    /// </summary>
    public static ChatbotMessageReceivedEvent MessageReceived(
        Guid sessionId,
        Guid userProfileId,
        string channel,
        string language,
        string userMessageContent,
        string assistantResponseContent,
        double responseLatencyMs,
        DateTimeOffset receivedAt)
    {
        return new ChatbotMessageReceivedEvent(
            Guid.NewGuid(),
            nameof(ChatbotMessageReceivedEvent),
            MessageType.Event,
            Version,
            Publisher,
            Consumers,
            sessionId,
            null,
            DateTimeOffset.UtcNow,
            true,
            new ChatbotMessageReceivedEventPayload(
                sessionId,
                userProfileId,
                channel,
                language,
                userMessageContent,
                assistantResponseContent,
                responseLatencyMs,
                receivedAt));
    }

    /// <summary>
    /// Creates a rate-limit-exceeded integration event.
    /// </summary>
    public static ChatbotRateLimitExceededEvent RateLimitExceeded(
        Guid sessionId,
        Guid userProfileId,
        string channel,
        int currentMessageCount,
        int rateLimitThreshold,
        DateTimeOffset resetAt)
    {
        return new ChatbotRateLimitExceededEvent(
            Guid.NewGuid(),
            nameof(ChatbotRateLimitExceededEvent),
            MessageType.Event,
            Version,
            Publisher,
            Consumers,
            sessionId,
            null,
            DateTimeOffset.UtcNow,
            true,
            new ChatbotRateLimitExceededEventPayload(
                userProfileId,
                sessionId,
                channel,
                currentMessageCount,
                rateLimitThreshold,
                resetAt));
    }
}
