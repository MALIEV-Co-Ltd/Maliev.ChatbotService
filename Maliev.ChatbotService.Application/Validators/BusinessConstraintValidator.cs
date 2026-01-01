using System.Text.Json;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Validators;

/// <summary>
/// Validates responses against business constraints and allowed topics.
/// </summary>
public class BusinessConstraintValidator
{
    private readonly ILogger<BusinessConstraintValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessConstraintValidator"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public BusinessConstraintValidator(ILogger<BusinessConstraintValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates if the user message is on-topic based on allowed topics.
    /// </summary>
    /// <param name="userMessage">The user message to validate.</param>
    /// <param name="systemInstruction">The system instruction with allowed topics.</param>
    /// <returns>True if on-topic; otherwise, false.</returns>
    public bool IsOnTopic(string userMessage, SystemInstruction systemInstruction)
    {
        if (string.IsNullOrWhiteSpace(systemInstruction.AllowedTopics))
        {
            return true;
        }

        var allowedTopics = systemInstruction.AllowedTopics
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var messageLower = userMessage.ToLowerInvariant();

        foreach (var topic in allowedTopics)
        {
            if (messageLower.Contains(topic.ToLowerInvariant()))
            {
                _logger.LogDebug("Message contains allowed topic: {Topic}", topic);
                return true;
            }
        }

        _logger.LogInformation("Message does not contain any allowed topics");
        return false;
    }

    /// <summary>
    /// Gets the appropriate rejection template for an off-topic request.
    /// </summary>
    /// <param name="userMessage">The user message.</param>
    /// <param name="systemInstruction">The system instruction with rejection templates.</param>
    /// <returns>The rejection message template.</returns>
    public string GetRejectionMessage(string userMessage, SystemInstruction systemInstruction)
    {
        if (string.IsNullOrWhiteSpace(systemInstruction.RejectionTemplates))
        {
            return GetDefaultRejectionMessage();
        }

        try
        {
            var templates = JsonSerializer.Deserialize<Dictionary<string, string>>(systemInstruction.RejectionTemplates);
            if (templates == null)
            {
                return GetDefaultRejectionMessage();
            }

            var messageLower = userMessage.ToLowerInvariant();

            if (messageLower.Contains("weather") || messageLower.Contains("rain") || messageLower.Contains("sunny"))
            {
                return templates.GetValueOrDefault("weather", GetDefaultRejectionMessage());
            }

            if (messageLower.Contains("competitor") || messageLower.Contains("other company") || messageLower.Contains("pricing"))
            {
                return templates.GetValueOrDefault("competitor", GetDefaultRejectionMessage());
            }

            return templates.GetValueOrDefault("general", GetDefaultRejectionMessage());
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse rejection templates JSON");
            return GetDefaultRejectionMessage();
        }
    }

    /// <summary>
    /// Validates that the response content aligns with business constraints.
    /// </summary>
    /// <param name="responseContent">The AI-generated response content.</param>
    /// <param name="systemInstruction">The system instruction with business constraints.</param>
    /// <returns>True if the response is compliant; otherwise, false.</returns>
    public bool IsResponseCompliant(string responseContent, SystemInstruction systemInstruction)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return false;
        }

        var responseLower = responseContent.ToLowerInvariant();

        var bannedPhrases = new[]
        {
            "i don't know",
            "i cannot help",
            "outside my expertise",
            "not my area"
        };

        foreach (var phrase in bannedPhrases)
        {
            if (responseLower.Contains(phrase))
            {
                _logger.LogWarning("Response contains non-compliant phrase: {Phrase}", phrase);
                return false;
            }
        }

        return true;
    }

    private static string GetDefaultRejectionMessage()
    {
        return @"I'm here to assist with manufacturing-related questions. I can help you with:
- Material selection and specifications
- Manufacturing process recommendations
- Order status and quotations
- Technical drawings and requirements
What would you like to know about our manufacturing services?";
    }
}
