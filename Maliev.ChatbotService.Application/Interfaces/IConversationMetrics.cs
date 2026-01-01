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
}
