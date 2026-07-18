using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="FallbackResponseTemplate"/> entity operations.
/// </summary>
public interface IFallbackResponseRepository
{
    /// <summary>
    /// Gets a fallback response template by scenario type and language.
    /// </summary>
    /// <param name="scenarioType">The scenario type.</param>
    /// <param name="language">The language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fallback response template if found; otherwise, null.</returns>
    Task<FallbackResponseTemplate?> GetByScenarioAsync(string scenarioType, Language language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new fallback response template.
    /// </summary>
    /// <param name="template">The template to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created template.</returns>
    Task<FallbackResponseTemplate> CreateAsync(FallbackResponseTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all fallback response templates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all templates.</returns>
    Task<List<FallbackResponseTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
}
