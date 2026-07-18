using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maliev.ChatbotService.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="SystemInstruction"/> entity operations.
/// </summary>
public class SystemInstructionRepository : ISystemInstructionRepository
{
    private readonly ChatbotDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public SystemInstructionRepository(ChatbotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction> CreateAsync(SystemInstruction instruction, CancellationToken cancellationToken = default)
    {
        _context.SystemInstructions.Add(instruction);
        await _context.SaveChangesAsync(cancellationToken);
        return instruction;
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SystemInstructions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveCoreAsync(CancellationToken cancellationToken = default)
    {
        return await GetActiveCoreAsync(null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveCoreAsync(string? topicKey, CancellationToken cancellationToken = default)
    {
        var normalizedTopicKey = NormalizeTopicKey(topicKey);
        var query = _context.SystemInstructions
            .Where(x => x.IsActive && x.Category == SystemInstructionCategory.Core);

        query = normalizedTopicKey is null
            ? query.Where(x => x.TopicKey == null || x.TopicKey == string.Empty)
            : query.Where(x => x.TopicKey == normalizedTopicKey);

        return await query.OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<SystemInstruction>> GetActiveByTopicsAsync(IEnumerable<string> topicKeys, CancellationToken cancellationToken = default)
    {
        return await _context.SystemInstructions
            .Where(x => x.IsActive
                && x.Category == SystemInstructionCategory.Topic
                && x.TopicKey != null
                && topicKeys.Contains(x.TopicKey))
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveAsync(SystemInstructionCategory? category = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemInstructions.Where(x => x.IsActive);

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        return await query
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<SystemInstruction> Instructions, int TotalCount)> GetAllAsync(int page, int pageSize, SystemInstructionCategory? category = null, string? topicKey = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemInstructions.AsQueryable();

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        if (!string.IsNullOrEmpty(topicKey))
        {
            query = query.Where(x => x.TopicKey == topicKey);
        }

        query = query.OrderByDescending(x => x.Version);

        var totalCount = await query.CountAsync(cancellationToken);
        var instructions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (instructions, totalCount);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(SystemInstruction instruction, CancellationToken cancellationToken = default)
    {
        _context.SystemInstructions.Update(instruction);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeactivateAllAsync(SystemInstructionCategory? category = null, string? topicKey = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemInstructions.Where(x => x.IsActive);

        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);

            var normalizedTopicKey = NormalizeTopicKey(topicKey);
            query = normalizedTopicKey is null
                ? query.Where(x => x.TopicKey == null || x.TopicKey == string.Empty)
                : query.Where(x => x.TopicKey == normalizedTopicKey);
        }
        else if (!string.IsNullOrWhiteSpace(topicKey))
        {
            var normalizedTopicKey = NormalizeTopicKey(topicKey);
            query = query.Where(x => x.TopicKey == normalizedTopicKey);
        }

        var activeInstructions = await query.ToListAsync(cancellationToken);

        foreach (var instruction in activeInstructions)
        {
            instruction.IsActive = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var instruction = await GetByIdAsync(id, cancellationToken);
        if (instruction != null)
        {
            _context.SystemInstructions.Remove(instruction);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<SystemInstruction?> GetActiveInstructionAsync(CancellationToken cancellationToken = default)
    {
        return GetActiveCoreAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var instruction = await GetByIdAsync(id, cancellationToken);
        if (instruction != null)
        {
            await DeactivateAllAsync(instruction.Category, instruction.TopicKey, cancellationToken);
            instruction.IsActive = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetByVersionAsync(int version, CancellationToken cancellationToken = default)
    {
        return await _context.SystemInstructions
            .FirstOrDefaultAsync(x => x.Version == version, cancellationToken);
    }

    private static string? NormalizeTopicKey(string? topicKey)
    {
        return string.IsNullOrWhiteSpace(topicKey) ? null : topicKey.Trim();
    }
}
