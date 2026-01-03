using System.Diagnostics.Metrics;
using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Infrastructure.Metrics;

/// <summary>
/// Provides business metrics for chatbot conversations and performance monitoring.
/// </summary>
public class ConversationMetrics : IConversationMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _conversationVolumeCounter;
    private readonly Histogram<double> _responseLatencyHistogram;
    private readonly ObservableGauge<double> _geminiApiSuccessRateGauge;
    private readonly ObservableGauge<int> _activeSessionsCountGauge;
    private readonly ObservableGauge<double> _userSatisfactionScoreGauge;
    private readonly Histogram<double> _intentClassificationConfidenceHistogram;
    private readonly Counter<long> _contextInjectionCounter;
    private readonly Counter<long> _cacheEventCounter;

    private double _geminiApiSuccessRate;
    private int _activeSessionsCount;
    private double _userSatisfactionScore;

    private readonly string _serviceName;
    private readonly string _version;
    private readonly string _region;
    private readonly string _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    /// <param name="configuration">The configuration.</param>
    public ConversationMetrics(IMeterFactory meterFactory, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _serviceName = "ChatbotService";
        _version = configuration["Service:Version"] ?? "1.0.0";
        _region = configuration["Service:Region"] ?? "unknown";
        _environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";

        _meter = meterFactory.Create("chatbot-service", _version);

        // Counter: Total number of conversations/messages processed
        _conversationVolumeCounter = _meter.CreateCounter<long>(
            name: "conversation_volume",
            unit: "messages",
            description: "Total number of chatbot messages processed");

        // Histogram: Response latency in milliseconds
        _responseLatencyHistogram = _meter.CreateHistogram<double>(
            name: "response_latency",
            unit: "ms",
            description: "Response latency for chatbot message processing in milliseconds");

        // Gauge: Gemini API success rate (0.0 to 1.0)
        _geminiApiSuccessRateGauge = _meter.CreateObservableGauge<double>(
            name: "gemini_api_success_rate",
            observeValue: () => new Measurement<double>(_geminiApiSuccessRate, GetStandardTags()),
            unit: "ratio",
            description: "Success rate of Gemini API calls (0.0 to 1.0)");

        // Gauge: Active sessions count
        _activeSessionsCountGauge = _meter.CreateObservableGauge<int>(
            name: "active_sessions_count",
            observeValue: () => new Measurement<int>(_activeSessionsCount, GetStandardTags()),
            unit: "sessions",
            description: "Number of active chatbot sessions");

        // Gauge: User satisfaction score (0.0 to 5.0)
        _userSatisfactionScoreGauge = _meter.CreateObservableGauge<double>(
            name: "user_satisfaction_score",
            observeValue: () => new Measurement<double>(_userSatisfactionScore, GetStandardTags()),
            unit: "score",
            description: "Average user satisfaction score (0.0 to 5.0)");

        // Histogram: Intent classification confidence
        _intentClassificationConfidenceHistogram = _meter.CreateHistogram<double>(
            name: "intent_classification_confidence",
            unit: "ratio",
            description: "Confidence scores for intent classification (0.0 to 1.0)");

        // Counter: Context injection volume
        _contextInjectionCounter = _meter.CreateCounter<long>(
            name: "context_injection_volume",
            unit: "injections",
            description: "Total number of context injections from Topic instructions or Knowledge Base");

        // Counter: Cache events (hits/misses)
        _cacheEventCounter = _meter.CreateCounter<long>(
            name: "chatbot_cache_events",
            unit: "events",
            description: "Instruction cache events tracked by type and result");

        // Initialize counter and histogram with zero values to ensure they appear in metrics output
        _conversationVolumeCounter.Add(0, GetStandardTags().ToArray());
        _responseLatencyHistogram.Record(0, GetStandardTags().ToArray());
        _intentClassificationConfidenceHistogram.Record(0, GetStandardTags().ToArray());
        _contextInjectionCounter.Add(0, GetStandardTags().ToArray());
        _cacheEventCounter.Add(0, GetStandardTags().ToArray());
    }

    /// <summary>
    /// Records a conversation volume metric.
    /// </summary>
    /// <param name="channel">The channel where the message was sent.</param>
    /// <param name="language">The language of the message.</param>
    public void RecordConversationVolume(string channel, string language)
    {
        var tags = GetStandardTags();
        tags.Add(new KeyValuePair<string, object?>("channel", channel));
        tags.Add(new KeyValuePair<string, object?>("language", language));

        _conversationVolumeCounter.Add(1, tags.ToArray());
    }

    /// <inheritdoc/>
    public void RecordConversation()
    {
        _conversationVolumeCounter.Add(1, GetStandardTags().ToArray());
    }

    /// <summary>
    /// Records a response latency metric.
    /// </summary>
    /// <param name="latencyMs">The response latency in milliseconds.</param>
    /// <param name="channel">The channel where the message was sent.</param>
    /// <param name="language">The language of the message.</param>
    public void RecordResponseLatency(double latencyMs, string channel, string language)
    {
        var tags = GetStandardTags();
        tags.Add(new KeyValuePair<string, object?>("channel", channel));
        tags.Add(new KeyValuePair<string, object?>("language", language));

        _responseLatencyHistogram.Record(latencyMs, tags.ToArray());
    }

    /// <inheritdoc/>
    public void RecordResponseLatency(double latencyMs)
    {
        _responseLatencyHistogram.Record(latencyMs, GetStandardTags().ToArray());
    }

    /// <summary>
    /// Updates the Gemini API success rate.
    /// </summary>
    /// <param name="successRate">The success rate (0.0 to 1.0).</param>
    public void UpdateGeminiApiSuccessRate(double successRate)
    {
        _geminiApiSuccessRate = Math.Clamp(successRate, 0.0, 1.0);
    }

    /// <summary>
    /// Updates the active sessions count.
    /// </summary>
    /// <param name="count">The number of active sessions.</param>
    public void UpdateActiveSessionsCount(int count)
    {
        _activeSessionsCount = Math.Max(0, count);
    }

    /// <inheritdoc/>
    public void UpdateUserSatisfactionScore(double score)
    {
        _userSatisfactionScore = Math.Clamp(score, 0.0, 5.0);
    }

    /// <inheritdoc/>
    public void RecordIntentClassification(string intent, double confidence)
    {
        var tags = GetStandardTags();
        tags.Add(new KeyValuePair<string, object?>("intent", intent));
        _intentClassificationConfidenceHistogram.Record(confidence, tags.ToArray());
    }

    /// <inheritdoc/>
    public void RecordContextInjection(string sourceType, string sourceId)
    {
        var tags = GetStandardTags();
        tags.Add(new KeyValuePair<string, object?>("source_type", sourceType));
        tags.Add(new KeyValuePair<string, object?>("source_id", sourceId));
        _contextInjectionCounter.Add(1, tags.ToArray());
    }

    /// <inheritdoc/>
    public void RecordCacheEvent(string cacheType, bool isHit)
    {
        var tags = GetStandardTags();
        tags.Add(new KeyValuePair<string, object?>("cache_type", cacheType));
        tags.Add(new KeyValuePair<string, object?>("result", isHit ? "hit" : "miss"));
        _cacheEventCounter.Add(1, tags.ToArray());
    }

    /// <summary>
    /// Gets the standard tags to be applied to all metrics.
    /// </summary>
    /// <returns>A list of standard tags.</returns>
    private List<KeyValuePair<string, object?>> GetStandardTags()
    {
        return new List<KeyValuePair<string, object?>>
        {
            new("service_name", _serviceName),
            new("version", _version),
            new("region", _region),
            new("environment", _environment)
        };
    }
}
