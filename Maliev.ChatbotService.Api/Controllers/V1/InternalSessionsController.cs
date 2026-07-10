using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Internal BFF-only session history endpoints.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/internal/sessions")]
public sealed class InternalSessionsController(
    IConversationSessionRepository sessionRepository,
    IMessageRepository messageRepository) : ControllerBase
{
    /// <summary>
    /// Gets ordered messages for a session by trusted BFF session ID.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversation messages.</returns>
    [HttpGet("{sessionId:guid}/messages")]
    [RequirePermission(ChatbotPermissions.SessionRead)]
    [ProducesResponseType(typeof(ConversationMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationMessagesResponse>> GetSessionMessages(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var messages = await messageRepository.GetMessagesBySessionIdAsync(session.Id, cancellationToken);
        return Ok(new ConversationMessagesResponse
        {
            SessionId = session.Id,
            UserProfileId = session.UserProfileId,
            Channel = ToWireValue(session.Channel),
            StartTime = session.StartTime,
            LastActivityAt = session.LastActivityAt,
            ExpiresAt = session.ExpiresAt,
            Language = ToWireValue(session.Language),
            Status = ToWireValue(session.Status),
            Messages = messages.Select(MapMessage).ToList()
        });
    }

    /// <summary>
    /// Deletes the last turn (most recent user message and everything after it) for a session, so an
    /// edited message can be resubmitted as a clean correction. Internal BFF-only; guarded by the same
    /// session permission the BFF already holds for reading internal session history.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete("{sessionId:guid}/messages/last-turn")]
    [RequirePermission(ChatbotPermissions.SessionRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLastTurn(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var removed = await messageRepository.DeleteLastTurnAsync(session.Id, cancellationToken);
        return Ok(new { removed });
    }

    private static ConversationMessageResponse MapMessage(Message message)
    {
        return new ConversationMessageResponse
        {
            MessageId = message.Id,
            Role = ToWireValue(message.Role),
            Content = message.Content,
            ContentType = ToWireValue(message.ContentType),
            CreatedAt = message.CreatedAt,
            GroundingProvenance = GroundingProvenanceResponseMapper.Map(
                message.Role == MessageRole.Assistant
                    ? MessageGroundingMetadata.TryReadProvenance(message.MetadataJson)
                    : null)
        };
    }

    private static string ToWireValue(Channel channel) => channel.ToString().ToLowerInvariant();

    private static string ToWireValue(Language language) => language == Language.Thai ? "th" : "en";

    private static string ToWireValue(SessionStatus status) => status.ToString().ToLowerInvariant();

    private static string ToWireValue(MessageRole role) => role.ToString().ToLowerInvariant();

    private static string ToWireValue(ContentType contentType) => contentType.ToString().ToLowerInvariant();
}
