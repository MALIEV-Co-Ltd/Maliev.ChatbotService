using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for conversation summary batch jobs.
/// </summary>
public class ConversationSummaryBatchJobRepository : IConversationSummaryBatchJobRepository
{
    private static readonly ConversationSummaryBatchStatus[] OpenStatuses =
    [
        ConversationSummaryBatchStatus.Pending,
        ConversationSummaryBatchStatus.Submitted
    ];

    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationSummaryBatchJobRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ConversationSummaryBatchJobRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<ConversationSummaryBatchJob> CreateAsync(
        ConversationSummaryBatchJob job,
        CancellationToken cancellationToken = default)
    {
        _context.ConversationSummaryBatchJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    /// <inheritdoc/>
    public async Task<ConversationSummaryBatchJob?> GetByBatchNameAsync(
        string batchName,
        CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSummaryBatchJobs
            .Include(x => x.Items.OrderBy(item => item.CreatedAt))
            .FirstOrDefaultAsync(x => x.BatchName == batchName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<ConversationSummaryBatchJob>> GetOpenJobsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSummaryBatchJobs
            .Include(x => x.Items.OrderBy(item => item.CreatedAt))
            .Where(x => OpenStatuses.Contains(x.Status))
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> HasOpenItemForSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSummaryBatchItems
            .AnyAsync(
                x => x.SessionId == sessionId && OpenStatuses.Contains(x.Status),
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(
        ConversationSummaryBatchJob job,
        CancellationToken cancellationToken = default)
    {
        _context.ConversationSummaryBatchJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
