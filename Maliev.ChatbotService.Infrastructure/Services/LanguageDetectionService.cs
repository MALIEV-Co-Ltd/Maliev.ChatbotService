using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using System.Text.RegularExpressions;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for detecting language in text (English vs Thai).
/// </summary>
public partial class LanguageDetectionService : ILanguageDetectionService
{
    /// <inheritdoc/>
    public Language DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Language.English;
        }

        // Thai Unicode range: 0E00-0E7F
        var thaiCharacterCount = ThaiCharacterRegex().Matches(text).Count;
        var totalCharacters = text.Replace(" ", "").Length;

        // If more than 30% of non-space characters are Thai, consider it Thai
        if (totalCharacters > 0 && (double)thaiCharacterCount / totalCharacters > 0.3)
        {
            return Language.Thai;
        }

        return Language.English;
    }

    /// <inheritdoc/>
    public Task<Language> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        // Delegate to synchronous method and wrap in Task
        return Task.FromResult(DetectLanguage(text));
    }

    [GeneratedRegex(@"[\u0E00-\u0E7F]")]
    private static partial Regex ThaiCharacterRegex();
}
