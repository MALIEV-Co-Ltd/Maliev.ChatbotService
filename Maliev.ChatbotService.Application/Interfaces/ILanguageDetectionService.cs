using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for language detection operations.
/// </summary>
public interface ILanguageDetectionService
{
    /// <summary>
    /// Detects the language of the given text.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>The detected language (English or Thai).</returns>
    Language DetectLanguage(string text);

    /// <summary>
    /// Detects the language of the given text asynchronously.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The detected language (English or Thai).</returns>
    Task<Language> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);
}
