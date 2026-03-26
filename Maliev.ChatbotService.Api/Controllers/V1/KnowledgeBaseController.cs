using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing knowledge base entries.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/admin/knowledge-base")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseController"/> class.
    /// </summary>
    /// <param name="repository">The knowledge base repository.</param>
    public KnowledgeBaseController(IKnowledgeBaseRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets a list of knowledge base entries.
    /// </summary>
    /// <param name="topicKey">Optional topic key filter.</param>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of knowledge base entries.</returns>
    [HttpGet]
    [RequirePermission(ChatbotPermissions.KnowledgeRead)]
    [ProducesResponseType(typeof(IEnumerable<KnowledgeBaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<KnowledgeBaseDto>>> GetEntries(
        [FromQuery] string? topicKey = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (entries, _) = await _repository.GetAllAsync(page, pageSize, topicKey, cancellationToken);
        return Ok(entries.Select(MapToDto));
    }

    /// <summary>
    /// Gets a specific knowledge base entry by ID.
    /// </summary>
    /// <param name="id">The entry ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The knowledge base entry.</returns>
    [HttpGet("{id}")]
    [RequirePermission(ChatbotPermissions.KnowledgeRead)]
    [ProducesResponseType(typeof(KnowledgeBaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeBaseDto>> GetEntry(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _repository.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound();
        }
        return Ok(MapToDto(entry));
    }

    /// <summary>
    /// Creates a new knowledge base entry.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created entry.</returns>
    [HttpPost]
    [RequirePermission(ChatbotPermissions.KnowledgeWrite)]
    [ProducesResponseType(typeof(KnowledgeBaseDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<KnowledgeBaseDto>> CreateEntry(
        [FromBody] CreateKnowledgeBaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = new KnowledgeBase
        {
            Id = Guid.NewGuid(),
            TopicKey = request.TopicKey,
            FactKey = request.FactKey,
            Content = request.Content,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : "{}"
        };

        await _repository.CreateAsync(entry, cancellationToken);

        return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, MapToDto(entry));
    }

    /// <summary>
    /// Updates an existing knowledge base entry.
    /// </summary>
    /// <param name="id">The entry ID.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated entry.</returns>
    [HttpPut("{id}")]
    [RequirePermission(ChatbotPermissions.KnowledgeWrite)]
    [ProducesResponseType(typeof(KnowledgeBaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeBaseDto>> UpdateEntry(
        Guid id,
        [FromBody] UpdateKnowledgeBaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = await _repository.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound();
        }

        if (request.TopicKey != null) entry.TopicKey = request.TopicKey;
        if (request.FactKey != null) entry.FactKey = request.FactKey;
        if (request.Content != null) entry.Content = request.Content;
        if (request.Metadata != null) entry.Metadata = JsonSerializer.Serialize(request.Metadata);

        await _repository.UpdateAsync(entry, cancellationToken);

        return Ok(MapToDto(entry));
    }

    /// <summary>
    /// Deletes a knowledge base entry.
    /// </summary>
    /// <param name="id">The entry ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    [RequirePermission(ChatbotPermissions.KnowledgeWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _repository.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, cancellationToken);

        return NoContent();
    }

    private static KnowledgeBaseDto MapToDto(KnowledgeBase entry)
    {
        return new KnowledgeBaseDto
        {
            Id = entry.Id,
            TopicKey = entry.TopicKey,
            FactKey = entry.FactKey,
            Content = entry.Content,
            Metadata = string.IsNullOrEmpty(entry.Metadata) ? null : JsonSerializer.Deserialize<object>(entry.Metadata)
        };
    }
}
