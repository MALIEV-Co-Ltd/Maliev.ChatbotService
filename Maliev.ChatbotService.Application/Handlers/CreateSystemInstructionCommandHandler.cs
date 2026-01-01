using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for creating a new system instruction.
/// </summary>
public class CreateSystemInstructionCommandHandler
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ILogger<CreateSystemInstructionCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSystemInstructionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="logger">The logger.</param>
    public CreateSystemInstructionCommandHandler(
        ISystemInstructionRepository repository,
        ILogger<CreateSystemInstructionCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the CreateSystemInstructionCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created system instruction.</returns>
    public async Task<SystemInstruction> HandleAsync(CreateSystemInstructionCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new system instruction: {Name}", command.Name);

        // If this instruction should be active, deactivate all others
        if (command.IsActive)
        {
            await _repository.DeactivateAllAsync(cancellationToken);
            _logger.LogInformation("Deactivated all existing system instructions");
        }

        // Get the next version number
        var (existingInstructions, _) = await _repository.GetAllAsync(1, int.MaxValue, cancellationToken);
        var nextVersion = existingInstructions.Any() ? existingInstructions.Max(i => i.Version) + 1 : 1;

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            PersonaDefinition = command.PersonaDefinition,
            BusinessConstraints = command.BusinessConstraints,
            IsActive = command.IsActive,
            Version = nextVersion
        };

        var created = await _repository.CreateAsync(instruction, cancellationToken);

        _logger.LogInformation("Created system instruction {Id} with version {Version}", created.Id, created.Version);

        return created;
    }
}
