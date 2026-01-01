using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="UserProfile"/> entity operations.
/// </summary>
public class UserProfileRepository : IUserProfileRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProfileRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public UserProfileRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<UserProfile> CreateAsync(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        _context.UserProfiles.Add(userProfile);
        await _context.SaveChangesAsync(cancellationToken);
        return userProfile;
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.LineUserId == lineUserId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetByFacebookIdAsync(string facebookId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.FacebookId == facebookId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetByInstagramIdAsync(string instagramId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.InstagramId == instagramId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetByWhatsAppIdAsync(string whatsAppId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.WhatsAppId == whatsAppId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<UserProfile?> GetByInternalUserIdAsync(string internalUserId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.InternalUserId == internalUserId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        _context.UserProfiles.Update(userProfile);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userProfile = await GetByIdAsync(id, cancellationToken);
        if (userProfile != null)
        {
            _context.UserProfiles.Remove(userProfile);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
