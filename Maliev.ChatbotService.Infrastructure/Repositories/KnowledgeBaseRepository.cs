using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="KnowledgeBase"/> entity operations.
/// </summary>
public class KnowledgeBaseRepository : IKnowledgeBaseRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public KnowledgeBaseRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeBase> CreateAsync(KnowledgeBase entry, CancellationToken cancellationToken = default)
    {
        entry.CreatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        _context.KnowledgeBase.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeBase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.KnowledgeBase
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<KnowledgeBase>> GetByTopicAsync(string topicKey, CancellationToken cancellationToken = default)
    {
        return await _context.KnowledgeBase
            .Where(x => x.TopicKey == topicKey)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeBase?> GetByFactKeyAsync(string topicKey, string factKey, CancellationToken cancellationToken = default)
    {
        return await _context.KnowledgeBase
            .FirstOrDefaultAsync(x => x.TopicKey == topicKey && x.FactKey == factKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<KnowledgeBase> Entries, int TotalCount)> GetAllAsync(int page, int pageSize, string? topicKey = null, CancellationToken cancellationToken = default)
    {
        var query = _context.KnowledgeBase.AsQueryable();

        if (!string.IsNullOrEmpty(topicKey))
        {
            query = query.Where(x => x.TopicKey == topicKey);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderBy(x => x.TopicKey)
            .ThenBy(x => x.FactKey)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entries, totalCount);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(KnowledgeBase entry, CancellationToken cancellationToken = default)
    {
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        _context.KnowledgeBase.Update(entry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry != null)
        {
            _context.KnowledgeBase.Remove(entry);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
