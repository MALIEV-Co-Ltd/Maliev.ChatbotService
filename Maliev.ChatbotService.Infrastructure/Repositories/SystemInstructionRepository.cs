using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
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
    public async Task<SystemInstruction?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SystemInstructions
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(List<SystemInstruction> Instructions, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemInstructions
            .OrderByDescending(x => x.Version);

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
    public async Task DeactivateAllAsync(CancellationToken cancellationToken = default)
    {
        var activeInstructions = await _context.SystemInstructions
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

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
        return GetActiveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DeactivateAllAsync(cancellationToken);

        var instruction = await GetByIdAsync(id, cancellationToken);
        if (instruction != null)
        {
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
}
