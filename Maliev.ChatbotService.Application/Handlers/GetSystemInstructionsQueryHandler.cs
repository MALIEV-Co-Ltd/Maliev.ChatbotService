using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Queries;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for getting system instructions.
/// </summary>
public class GetSystemInstructionsQueryHandler
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ILogger<GetSystemInstructionsQueryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetSystemInstructionsQueryHandler"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="logger">The logger.</param>
    public GetSystemInstructionsQueryHandler(
        ISystemInstructionRepository repository,
        ILogger<GetSystemInstructionsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the GetSystemInstructionsQuery.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of system instructions.</returns>
    public async Task<List<SystemInstruction>> HandleAsync(GetSystemInstructionsQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting system instructions (activeOnly: {ActiveOnly}, category: {Category}, topicKey: {TopicKey})", query.ActiveOnly, query.Category, query.TopicKey);

        var (instructions, _) = await _repository.GetAllAsync(1, int.MaxValue, query.Category, query.TopicKey, cancellationToken);

        if (query.ActiveOnly)
        {
            instructions = instructions.Where(i => i.IsActive).ToList();
        }

        _logger.LogInformation("Retrieved {Count} system instructions", instructions.Count);

        return instructions;
    }
}
