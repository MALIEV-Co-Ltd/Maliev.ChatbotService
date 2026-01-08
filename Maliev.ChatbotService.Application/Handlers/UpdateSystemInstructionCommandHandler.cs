using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for updating an existing system instruction.
/// </summary>
public class UpdateSystemInstructionCommandHandler
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ILogger<UpdateSystemInstructionCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSystemInstructionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="logger">The logger.</param>
    public UpdateSystemInstructionCommandHandler(
        ISystemInstructionRepository repository,
        ILogger<UpdateSystemInstructionCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the UpdateSystemInstructionCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated system instruction.</returns>
    /// <exception cref="InvalidOperationException">Thrown when instruction is not found.</exception>
    public async Task<SystemInstruction> HandleAsync(UpdateSystemInstructionCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating system instruction {Id}", command.Id);

        var instruction = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (instruction == null)
        {
            throw new InvalidOperationException($"System instruction {command.Id} not found");
        }

        // If setting this instruction to active, deactivate all others in the same category/topic
        if (command.IsActive == true && !instruction.IsActive)
        {
            await _repository.DeactivateAllAsync(
                command.Category ?? instruction.Category,
                command.TopicKey ?? instruction.TopicKey,
                cancellationToken);
            _logger.LogInformation("Deactivated existing system instructions for Category and TopicKey");
        }

        // Update fields if provided
        if (command.Name != null)
        {
            instruction.Name = command.Name;
        }

        if (command.Category.HasValue)
        {
            instruction.Category = command.Category.Value;
        }

        if (command.TopicKey != null)
        {
            instruction.TopicKey = command.TopicKey;
        }

        if (command.Priority.HasValue)
        {
            instruction.Priority = command.Priority.Value;
        }

        if (command.PersonaDefinition != null)
        {
            instruction.PersonaDefinition = command.PersonaDefinition;
        }

        if (command.BusinessConstraints != null)
        {
            instruction.BusinessConstraints = command.BusinessConstraints;
        }

        if (command.IsActive.HasValue)
        {
            instruction.IsActive = command.IsActive.Value;
        }

        // Increment version
        instruction.Version++;

        await _repository.UpdateAsync(instruction, cancellationToken);

        _logger.LogInformation("Updated system instruction {Id} to version {Version}", instruction.Id, instruction.Version);

        return instruction;
    }
}
