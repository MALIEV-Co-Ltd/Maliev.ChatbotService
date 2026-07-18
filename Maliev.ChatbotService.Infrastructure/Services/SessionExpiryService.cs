using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for processing expired chatbot sessions.
/// </summary>
public class SessionExpiryService : ISessionExpiryService
{
    private readonly ChatbotDbContext _dbContext;
    private readonly ILogger<SessionExpiryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExpiryService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    public SessionExpiryService(ChatbotDbContext dbContext, ILogger<SessionExpiryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var expiredSessions = await _dbContext.ConversationSessions
            .Where(s => s.Status == SessionStatus.Active && s.ExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (expiredSessions.Count == 0)
        {
            _logger.LogDebug("No expired sessions to process");
            return;
        }

        foreach (var session in expiredSessions)
        {
            session.Status = SessionStatus.Closed;
            _logger.LogInformation("Marked session {SessionId} as closed due to expiration", session.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Processed {Count} expired sessions", expiredSessions.Count);
    }
}
