using Maliev.ChatbotService.Application.Models;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for extracting user preferences from conversation messages.
/// </summary>
public interface IExtractPreferencesService
{
    /// <summary>
    /// Extracts preferences from a user message.
    /// </summary>
    /// <param name="message">The user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of extracted preferences with confidence scores.</returns>
    Task<List<ExtractedPreference>> ExtractPreferencesAsync(string message, CancellationToken cancellationToken = default);
}
