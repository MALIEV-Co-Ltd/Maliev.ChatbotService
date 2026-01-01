using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="IdentityLink"/> entity operations.
/// </summary>
public class IdentityLinkRepository : IIdentityLinkRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityLinkRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public IdentityLinkRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<IdentityLink> CreateAsync(IdentityLink identityLink, CancellationToken cancellationToken = default)
    {
        _context.IdentityLinks.Add(identityLink);
        await _context.SaveChangesAsync(cancellationToken);
        return identityLink;
    }

    /// <inheritdoc/>
    public async Task<IdentityLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityLinks
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IdentityLink?> GetByPlatformIdAsync(PlatformName platformName, string externalPlatformId, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityLinks
            .FirstOrDefaultAsync(x => x.PlatformName == platformName && x.ExternalPlatformId == externalPlatformId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<IdentityLink>> GetByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return await _context.IdentityLinks
            .Where(x => x.UserProfileId == userProfileId)
            .OrderByDescending(x => x.LinkCreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(IdentityLink identityLink, CancellationToken cancellationToken = default)
    {
        _context.IdentityLinks.Update(identityLink);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identityLink = await GetByIdAsync(id, cancellationToken);
        if (identityLink != null)
        {
            _context.IdentityLinks.Remove(identityLink);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<List<IdentityLink>> GetLinksByUserIdAsync(Guid userProfileId, CancellationToken cancellationToken = default)
    {
        return GetByUserIdAsync(userProfileId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveLinkAsync(Guid userProfileId, PlatformName platformName, CancellationToken cancellationToken = default)
    {
        var link = await _context.IdentityLinks
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId && x.PlatformName == platformName, cancellationToken);

        if (link != null)
        {
            _context.IdentityLinks.Remove(link);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
