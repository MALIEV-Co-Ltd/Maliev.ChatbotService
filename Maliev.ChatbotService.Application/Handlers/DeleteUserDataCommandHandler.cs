using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Handler for deleting user data with specified scope.
/// </summary>
public class DeleteUserDataCommandHandler
{
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IOperationLogRepository _operationLogRepository;
    private readonly ILogger<DeleteUserDataCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteUserDataCommandHandler"/> class.
    /// </summary>
    /// <param name="userMemoryRepository">The user memory repository.</param>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="messageRepository">The message repository.</param>
    /// <param name="operationLogRepository">The operation log repository.</param>
    /// <param name="logger">The logger.</param>
    public DeleteUserDataCommandHandler(
        IUserMemoryRepository userMemoryRepository,
        IConversationSessionRepository sessionRepository,
        IMessageRepository messageRepository,
        IOperationLogRepository operationLogRepository,
        ILogger<DeleteUserDataCommandHandler> logger)
    {
        _userMemoryRepository = userMemoryRepository;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _operationLogRepository = operationLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Handles the DeleteUserDataCommand.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when confirmation is required but not provided.</exception>
    public async Task HandleAsync(DeleteUserDataCommand command, CancellationToken cancellationToken = default)
    {
        if (!command.Confirm)
        {
            throw new InvalidOperationException("Deletion must be confirmed by setting Confirm=true");
        }

        var scope = command.Scope.ToLowerInvariant();

        _logger.LogInformation("Deleting {Scope} data for user {UserProfileId}", scope, command.UserProfileId);

        switch (scope)
        {
            case "preferences":
                await DeletePreferencesAsync(command.UserProfileId, cancellationToken);
                break;

            case "history":
                await DeleteHistoryAsync(command.UserProfileId, cancellationToken);
                break;

            case "all":
                await DeleteAllDataAsync(command.UserProfileId, cancellationToken);
                break;

            default:
                throw new ArgumentException($"Invalid scope: {command.Scope}. Valid values are: preferences, history, all", nameof(command.Scope));
        }

        await _operationLogRepository.CreateAsync(new OperationLog
        {
            Id = Guid.NewGuid(),
            UserProfileId = command.UserProfileId,
            MessageId = null,
            OperationType = "UserDataDeletion",
            OperationParameters = $"{{\"scope\":\"{scope}\"}}",
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = true
        }, cancellationToken);

        _logger.LogInformation("Successfully deleted {Scope} data for user {UserProfileId}", scope, command.UserProfileId);
    }

    private async Task DeletePreferencesAsync(Guid userProfileId, CancellationToken cancellationToken)
    {
        await _userMemoryRepository.DeleteByUserIdAsync(userProfileId, cancellationToken);
        _logger.LogInformation("Deleted all preferences for user {UserProfileId}", userProfileId);
    }

    private async Task DeleteHistoryAsync(Guid userProfileId, CancellationToken cancellationToken)
    {
        var (sessions, _) = await _sessionRepository.GetByUserIdAsync(userProfileId, 1, 1000, cancellationToken);

        foreach (var session in sessions)
        {
            var messages = await _messageRepository.GetBySessionIdAsync(session.Id, cancellationToken);
            foreach (var message in messages)
            {
                await _messageRepository.DeleteAsync(message.Id, cancellationToken);
            }
            await _sessionRepository.DeleteAsync(session.Id, cancellationToken);
        }

        _logger.LogInformation("Deleted conversation history for user {UserProfileId} ({SessionCount} sessions)",
            userProfileId, sessions.Count);
    }

    private async Task DeleteAllDataAsync(Guid userProfileId, CancellationToken cancellationToken)
    {
        await DeletePreferencesAsync(userProfileId, cancellationToken);
        await DeleteHistoryAsync(userProfileId, cancellationToken);
        _logger.LogInformation("Deleted all data for user {UserProfileId}", userProfileId);
    }
}
