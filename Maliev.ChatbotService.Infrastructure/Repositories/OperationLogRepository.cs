using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="OperationLog"/> entity operations.
/// </summary>
public class OperationLogRepository : IOperationLogRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationLogRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public OperationLogRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<OperationLog> CreateAsync(OperationLog operationLog, CancellationToken cancellationToken = default)
    {
        _context.OperationLogs.Add(operationLog);
        await _context.SaveChangesAsync(cancellationToken);
        return operationLog;
    }

    /// <inheritdoc/>
    public async Task<OperationLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OperationLogs
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<OperationLog> Logs, int TotalCount)> GetByUserIdAsync(Guid userProfileId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.OperationLogs
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.ExecutedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (logs, totalCount);
    }

    /// <inheritdoc/>
    public async Task<List<OperationLog>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _context.OperationLogs
            .Where(x => x.MessageId == messageId)
            .OrderByDescending(x => x.ExecutedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(OperationLog operationLog, CancellationToken cancellationToken = default)
    {
        _context.OperationLogs.Update(operationLog);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operationLog = await GetByIdAsync(id, cancellationToken);
        if (operationLog != null)
        {
            _context.OperationLogs.Remove(operationLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<List<OperationLog>> GetLogsByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.OperationLogs
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.ExecutedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<OperationLog>> GetLogsByOperationTypeAsync(string operationType, CancellationToken cancellationToken = default)
    {
        return await _context.OperationLogs
            .Where(x => x.OperationType == operationType)
            .OrderByDescending(x => x.ExecutedAt)
            .ToListAsync(cancellationToken);
    }
}
