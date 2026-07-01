using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="Message"/> entity operations.
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public MessageRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<Message> CreateAsync(Message message, CancellationToken cancellationToken = default)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    /// <inheritdoc/>
    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Message>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .CountAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Message>> GetRecentBySessionIdAsync(Guid sessionId, int count, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Message message, CancellationToken cancellationToken = default)
    {
        _context.Messages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await GetByIdAsync(id, cancellationToken);
        if (message != null)
        {
            _context.Messages.Remove(message);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<List<Message>> GetMessagesBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await GetBySessionIdAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteLastTurnAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var messages = await _context.Messages
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // The "last turn" is the most recent user message plus everything that followed it.
        var lastUserIndex = messages.FindLastIndex(x => x.Role == MessageRole.User);
        if (lastUserIndex < 0)
        {
            return 0;
        }

        var toRemove = messages.GetRange(lastUserIndex, messages.Count - lastUserIndex);
        _context.Messages.RemoveRange(toRemove);
        await _context.SaveChangesAsync(cancellationToken);
        return toRemove.Count;
    }
}
