using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing system instructions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("chatbot/v{version:apiVersion}/admin/instructions")]
[Authorize]
public class SystemInstructionsController : ControllerBase
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ISystemInstructionService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionsController"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="service">The system instruction service.</param>
    public SystemInstructionsController(ISystemInstructionRepository repository, ISystemInstructionService service)
    {
        _repository = repository;
        _service = service;
    }

    /// <summary>
    /// Gets a list of system instructions.
    /// </summary>
    /// <param name="category">Optional category filter.</param>
    /// <param name="topicKey">Optional topic key filter.</param>
    /// <param name="activeOnly">If set to true, returns only active instructions.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of system instructions.</returns>
    [HttpGet]
    [RequirePermission("chatbot.instructions.read")]
    [ProducesResponseType(typeof(IEnumerable<SystemInstructionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SystemInstructionDto>>> GetInstructions(
        [FromQuery] SystemInstructionCategory? category = null,
        [FromQuery] string? topicKey = null,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (activeOnly)
        {
            var active = await _repository.GetActiveAsync(category, cancellationToken);
            if (active == null)
            {
                return Ok(Array.Empty<SystemInstructionDto>());
            }
            return Ok(new[] { MapToDto(active) });
        }

        var (instructions, _) = await _repository.GetAllAsync(page, pageSize, category, topicKey, cancellationToken);
        return Ok(instructions.Select(MapToDto));
    }

    /// <summary>
    /// Creates a new system instruction.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created instruction.</returns>
    [HttpPost]
    [RequirePermission("chatbot.instructions.write")]
    [ProducesResponseType(typeof(SystemInstructionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SystemInstructionDto>> CreateInstruction(
        [FromBody] CreateSystemInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Category = request.Category,
            TopicKey = request.TopicKey,
            Priority = request.Priority,
            PersonaDefinition = request.PersonaDefinition,
            BusinessConstraints = request.BusinessConstraints,
            IsActive = request.IsActive,
            Version = 1,
            AllowedTopics = string.Empty,
            RejectionTemplates = "{}"
        };

        if (instruction.IsActive)
        {
            await _repository.DeactivateAllAsync(instruction.Category, instruction.TopicKey, cancellationToken);
        }

        await _repository.CreateAsync(instruction, cancellationToken);
        await _service.InvalidateCacheAsync(cancellationToken);

        return CreatedAtAction(nameof(GetInstructions), new { id = instruction.Id }, MapToDto(instruction));
    }

    /// <summary>
    /// Updates an existing system instruction.
    /// </summary>
    /// <param name="id">The instruction ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated instruction.</returns>
    [HttpPut("{id}")]
    [RequirePermission("chatbot.instructions.write")]
    [ProducesResponseType(typeof(SystemInstructionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemInstructionDto>> UpdateInstruction(
        Guid id,
        [FromBody] UpdateSystemInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        var instruction = await _repository.GetByIdAsync(id, cancellationToken);
        if (instruction == null)
        {
            return NotFound();
        }

        if (request.Name != null) instruction.Name = request.Name;
        if (request.Category.HasValue) instruction.Category = request.Category.Value;
        if (request.TopicKey != null) instruction.TopicKey = request.TopicKey;
        if (request.Priority.HasValue) instruction.Priority = request.Priority.Value;
        if (request.PersonaDefinition != null) instruction.PersonaDefinition = request.PersonaDefinition;
        if (request.BusinessConstraints != null) instruction.BusinessConstraints = request.BusinessConstraints;
        
        if (request.IsActive.HasValue)
        {
             if (request.IsActive.Value && !instruction.IsActive)
             {
                 await _repository.DeactivateAllAsync(instruction.Category, instruction.TopicKey, cancellationToken);
             }
             instruction.IsActive = request.IsActive.Value;
        }

        instruction.Version++; // Increment version on update

        await _repository.UpdateAsync(instruction, cancellationToken);
        await _service.InvalidateCacheAsync(cancellationToken);

        return Ok(MapToDto(instruction));
    }

    /// <summary>
    /// Deletes a system instruction.
    /// </summary>
    /// <param name="id">The instruction ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    [RequirePermission("chatbot.instructions.write")]
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
