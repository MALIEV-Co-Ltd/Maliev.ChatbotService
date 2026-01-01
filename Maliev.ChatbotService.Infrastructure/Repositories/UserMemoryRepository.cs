using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="UserMemory"/> entity operations.
/// </summary>
public class UserMemoryRepository : IUserMemoryRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserMemoryRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public UserMemoryRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<UserMemory> CreateAsync(UserMemory userMemory, CancellationToken cancellationToken = default)
    {
        _context.UserMemories.Add(userMemory);
        await _context.SaveChangesAsync(cancellationToken);
        return userMemory;
    }

    /// <inheritdoc/>
    public async Task<UserMemory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserMemories
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<UserMemory>> GetByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.UserMemories
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<UserMemory> Memories, int TotalCount)> GetByUserIdAsync(Guid userProfileId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.UserMemories
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.LastUpdatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var memories = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (memories, totalCount);
    }

    /// <inheritdoc/>
    public async Task<List<UserMemory>> GetHighConfidenceMemoriesAsync(Guid userProfileId, double confidenceThreshold, CancellationToken cancellationToken = default)
    {
        return await _context.UserMemories
            .Where(x => x.UserProfileId == userProfileId && x.Confidence >= confidenceThreshold)
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(UserMemory userMemory, CancellationToken cancellationToken = default)
    {
        _context.UserMemories.Update(userMemory);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userMemory = await GetByIdAsync(id, cancellationToken);
        if (userMemory != null)
        {
            _context.UserMemories.Remove(userMemory);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        var memories = await _context.UserMemories
            .Where(x => x.UserProfileId == userProfileId)
            .ToListAsync(cancellationToken);

        _context.UserMemories.RemoveRange(memories);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserMemory?> GetMemoryByKeyAsync(Guid userProfileId, string key, CancellationToken cancellationToken = default)
    {
        return await _context.UserMemories
            .Where(x => x.UserProfileId == userProfileId && x.Key == key)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<UserMemory>> GetMemoriesByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await GetByUserIdAsync(userProfileId, cancellationToken);
    }
}
