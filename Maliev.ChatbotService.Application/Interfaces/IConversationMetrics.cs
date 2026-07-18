namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Interface for recording conversation metrics.
/// </summary>
public interface IConversationMetrics
{
    /// <summary>
    /// Records a new conversation message.
    /// </summary>
    void RecordConversation();

    /// <summary>
    /// Records the response latency for a conversation.
    /// </summary>
    /// <param name="latencyMs">The response latency in milliseconds.</param>
    void RecordResponseLatency(double latencyMs);

    /// <summary>
    /// Updates the Gemini API success rate.
    /// </summary>
    /// <param name="successRate">The success rate (0.0 to 1.0).</param>
    void UpdateGeminiApiSuccessRate(double successRate);

    /// <summary>
    /// Updates the count of active sessions.
    /// </summary>
    /// <param name="count">The number of active sessions.</param>
    void UpdateActiveSessionsCount(int count);

    /// <summary>
    /// Updates the user satisfaction score.
    /// </summary>
    /// <param name="score">The satisfaction score (0.0 to 1.0).</param>
    void UpdateUserSatisfactionScore(double score);

    /// <summary>
    /// Records an intent classification event with its confidence score.
    /// </summary>
    /// <param name="intent">The detected intent.</param>
    /// <param name="confidence">The confidence score.</param>
    void RecordIntentClassification(string intent, double confidence);

    /// <summary>
    /// Records a context injection event from Topic instructions or Knowledge Base.
    /// </summary>
    /// <param name="sourceType">The source of the context (Topic, KnowledgeBase).</param>
    /// <param name="sourceId">The ID or key of the source.</param>
    void RecordContextInjection(string sourceType, string sourceId);

    /// <summary>
    /// Records a cache hit or miss for system instructions.
    /// </summary>
    /// <param name="cacheType">The type of cache (Merged, Entity).</param>
    /// <param name="isHit">True if it was a cache hit.</param>
    void RecordCacheEvent(string cacheType, bool isHit);
}
