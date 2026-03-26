using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Queries;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing system instructions.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/admin/instructions")]
[Authorize]
public class SystemInstructionsController : ControllerBase
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ISystemInstructionService _service;
    private readonly CreateSystemInstructionCommandHandler _createHandler;
    private readonly UpdateSystemInstructionCommandHandler _updateHandler;
    private readonly GetSystemInstructionsQueryHandler _getQueryHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionsController"/> class.
    /// </summary>
    public SystemInstructionsController(
        ISystemInstructionRepository repository,
        ISystemInstructionService service,
        CreateSystemInstructionCommandHandler createHandler,
        UpdateSystemInstructionCommandHandler updateHandler,
        GetSystemInstructionsQueryHandler getQueryHandler)
    {
        _repository = repository;
        _service = service;
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _getQueryHandler = getQueryHandler;
    }

    /// <summary>
    /// Gets a list of system instructions.
    /// </summary>
    [HttpGet]
    [RequirePermission(ChatbotPermissions.InstructionsRead)]
    [ProducesResponseType(typeof(IEnumerable<SystemInstructionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SystemInstructionDto>>> GetInstructions(
        [FromQuery] SystemInstructionCategory? category = null,
        [FromQuery] string? topicKey = null,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSystemInstructionsQuery
        {
            Category = category,
            TopicKey = topicKey,
            ActiveOnly = activeOnly
        };

        var instructions = await _getQueryHandler.HandleAsync(query, cancellationToken);

        // Manual pagination if needed, but QueryHandler currently returns all
        var pagedInstructions = instructions
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return Ok(pagedInstructions.Select(MapToDto));
    }

    /// <summary>
    /// Creates a new system instruction.
    /// </summary>
    [HttpPost]
    [RequirePermission(ChatbotPermissions.InstructionsWrite)]
    [ProducesResponseType(typeof(SystemInstructionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SystemInstructionDto>> CreateInstruction(
        [FromBody] CreateSystemInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateSystemInstructionCommand
        {
            Name = request.Name,
            Category = request.Category,
            TopicKey = request.TopicKey,
            Priority = request.Priority,
            PersonaDefinition = request.PersonaDefinition,
            BusinessConstraints = request.BusinessConstraints,
            IsActive = request.IsActive
        };

        var instruction = await _createHandler.HandleAsync(command, cancellationToken);
        await _service.InvalidateCacheAsync(cancellationToken);

        return CreatedAtAction(nameof(GetInstructions), new { id = instruction.Id }, MapToDto(instruction));
    }

    /// <summary>
    /// Updates an existing system instruction.
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(ChatbotPermissions.InstructionsWrite)]
    [ProducesResponseType(typeof(SystemInstructionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemInstructionDto>> UpdateInstruction(
        Guid id,
        [FromBody] UpdateSystemInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new UpdateSystemInstructionCommand
            {
                Id = id,
                Name = request.Name,
                Category = request.Category,
                TopicKey = request.TopicKey,
                Priority = request.Priority,
                PersonaDefinition = request.PersonaDefinition,
                BusinessConstraints = request.BusinessConstraints,
                IsActive = request.IsActive
            };

            var instruction = await _updateHandler.HandleAsync(command, cancellationToken);
            await _service.InvalidateCacheAsync(cancellationToken);

            return Ok(MapToDto(instruction));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a system instruction.
    /// </summary>
    /// <param name="id">The instruction ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    [RequirePermission(ChatbotPermissions.InstructionsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInstruction(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var instruction = await _repository.GetByIdAsync(id, cancellationToken);
        if (instruction == null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, cancellationToken);
        await _service.InvalidateCacheAsync(cancellationToken);

        return NoContent();
    }

    private static SystemInstructionDto MapToDto(SystemInstruction instruction)
    {
        return new SystemInstructionDto
        {
            Id = instruction.Id,
            Name = instruction.Name,
            Category = instruction.Category,
            TopicKey = instruction.TopicKey,
            Priority = instruction.Priority,
            PersonaDefinition = instruction.PersonaDefinition,
            BusinessConstraints = instruction.BusinessConstraints,
            IsActive = instruction.IsActive,
            Version = instruction.Version
        };
    }
}
