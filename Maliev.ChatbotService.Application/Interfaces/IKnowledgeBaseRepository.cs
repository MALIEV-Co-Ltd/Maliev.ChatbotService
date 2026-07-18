using Maliev.ChatbotService.Domain.Entities;

namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Repository interface for <see cref="KnowledgeBase"/> entity operations.
/// </summary>
public interface IKnowledgeBaseRepository
{
    /// <summary>
    /// Creates a new knowledge base entry.
    /// </summary>
    /// <param name="entry">The entry to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created entry.</returns>
    Task<KnowledgeBase> CreateAsync(KnowledgeBase entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a knowledge base entry by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry if found; otherwise, null.</returns>
    Task<KnowledgeBase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all knowledge base entries for a specific topic.
    /// </summary>
    /// <param name="topicKey">The topic key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of knowledge base entries.</returns>
    Task<List<KnowledgeBase>> GetByTopicAsync(string topicKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific fact by topic and fact key.
    /// </summary>
    /// <param name="topicKey">The topic key.</param>
    /// <param name="factKey">The fact key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry if found; otherwise, null.</returns>
    Task<KnowledgeBase?> GetByFactKeyAsync(string topicKey, string factKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all knowledge base entries with pagination and optional filtering.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="topicKey">Optional topic key filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of entries list and total count.</returns>
    Task<(List<KnowledgeBase> Entries, int TotalCount)> GetAllAsync(int page, int pageSize, string? topicKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a knowledge base entry.
    /// </summary>
    /// <param name="entry">The entry to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(KnowledgeBase entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a knowledge base entry.
    /// </summary>
    /// <param name="id">The unique identifier of the entry to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
