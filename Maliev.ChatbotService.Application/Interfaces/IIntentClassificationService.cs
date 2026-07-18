namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Interface for classifying user intent into specialized topics.
/// </summary>
public interface IIntentClassificationService
{
    /// <summary>
    /// Classifies the user message into a list of topic keys.
    /// </summary>
    /// <param name="message">The user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A classification result containing detected intent and confidence.</returns>
    Task<IntentClassificationResult> ClassifyIntentAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of intent classification.
/// </summary>
public class IntentClassificationResult
{
    /// <summary>
    /// Gets or sets the detected intent (topic key).
    /// </summary>
    public string Intent { get; set; } = "General";

    /// <summary>
    /// Gets or sets the confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets additional topics detected if confidence is high.
    /// </summary>
    public List<string> AdditionalTopics { get; set; } = new();
}
