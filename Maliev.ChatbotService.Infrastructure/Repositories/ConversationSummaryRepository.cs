using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for conversation summary operations.
/// </summary>
public class ConversationSummaryRepository : IConversationSummaryRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationSummaryRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ConversationSummaryRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new conversation summary.
    /// </summary>
    /// <param name="summary">The conversation summary to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation summary.</returns>
    public async Task<ConversationSummary> CreateAsync(ConversationSummary summary, CancellationToken cancellationToken = default)
    {
        _context.ConversationSummaries.Add(summary);
        await _context.SaveChangesAsync(cancellationToken);
        return summary;
    }

    /// <summary>
    /// Retrieves a conversation summary by session ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation summary if found; otherwise, null.</returns>
    public async Task<ConversationSummary?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSummaries
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
    }

    /// <summary>
    /// Retrieves recent conversation summaries for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="limit">The maximum number of summaries to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection of conversation summaries.</returns>
    public async Task<IEnumerable<ConversationSummary>> GetRecentByUserProfileIdAsync(Guid userProfileId, int limit, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSummaries
            .Where(s => s.UserProfileId == userProfileId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Updates an existing conversation summary.
    /// </summary>
    /// <param name="summary">The conversation summary to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated conversation summary.</returns>
    public async Task<ConversationSummary> UpdateAsync(ConversationSummary summary, CancellationToken cancellationToken = default)
    {
        _context.ConversationSummaries.Update(summary);
        await _context.SaveChangesAsync(cancellationToken);
        return summary;
    }
}
