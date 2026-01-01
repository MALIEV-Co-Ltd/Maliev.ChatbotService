using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="ConversationSession"/> entity operations.
/// </summary>
public class ConversationSessionRepository : IConversationSessionRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationSessionRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ConversationSessionRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<ConversationSession> CreateAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        _context.ConversationSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    /// <inheritdoc/>
    public async Task<ConversationSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSessions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<ConversationSession>> GetActiveSessionsByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSessions
            .Where(x => x.UserProfileId == userProfileId && x.Status == SessionStatus.Active)
            .OrderByDescending(x => x.LastActivityAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<ConversationSession>> GetExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _context.ConversationSessions
            .Where(x => x.Status == SessionStatus.Active && x.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<ConversationSession> Sessions, int TotalCount)> GetByUserIdAsync(Guid userProfileId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.ConversationSessions
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.StartTime);

        var totalCount = await query.CountAsync(cancellationToken);
        var sessions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (sessions, totalCount);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        _context.ConversationSessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(id, cancellationToken);
        if (session != null)
        {
            _context.ConversationSessions.Remove(session);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<List<ConversationSession>> GetSessionsByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSessions
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.StartTime)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ConversationSession?> GetActiveSessionByUserAndChannelAsync(Guid userProfileId, PlatformName channelName, CancellationToken cancellationToken = default)
    {
        // Map PlatformName to Channel
        var channel = channelName switch
        {
            PlatformName.Line => Channel.Line,
            PlatformName.Facebook => Channel.Facebook,
            PlatformName.Instagram => Channel.Instagram,
            PlatformName.WhatsApp => Channel.WhatsApp,
            _ => throw new ArgumentException($"Unknown platform name: {channelName}", nameof(channelName))
        };

        return await _context.ConversationSessions
            .Where(x => x.UserProfileId == userProfileId && x.Channel == channel && x.Status == SessionStatus.Active)
            .OrderByDescending(x => x.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetByIdAsync(sessionId, cancellationToken);
        if (session != null)
        {
            session.Status = SessionStatus.Closed;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetActiveSessionsCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ConversationSessions
            .CountAsync(x => x.Status == SessionStatus.Active, cancellationToken);
    }
}
