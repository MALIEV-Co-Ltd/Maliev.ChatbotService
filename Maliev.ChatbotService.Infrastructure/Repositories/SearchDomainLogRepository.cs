using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="SearchDomainLog"/> entity operations.
/// </summary>
public class SearchDomainLogRepository : ISearchDomainLogRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDomainLogRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public SearchDomainLogRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<SearchDomainLog> CreateAsync(SearchDomainLog searchDomainLog, CancellationToken cancellationToken = default)
    {
        _context.SearchDomainLogs.Add(searchDomainLog);
        await _context.SaveChangesAsync(cancellationToken);
        return searchDomainLog;
    }

    /// <inheritdoc/>
    public async Task<SearchDomainLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SearchDomainLogs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<SearchDomainLog>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.SearchDomainLogs
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.AccessedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<SearchDomainLog> Logs, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.SearchDomainLogs
            .OrderByDescending(x => x.AccessedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (logs, totalCount);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var searchDomainLog = await GetByIdAsync(id, cancellationToken);
        if (searchDomainLog != null)
        {
            _context.SearchDomainLogs.Remove(searchDomainLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<List<SearchDomainLog>> GetRecentLogsAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _context.SearchDomainLogs
            .OrderByDescending(x => x.AccessedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
