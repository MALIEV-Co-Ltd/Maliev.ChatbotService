using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for managing chatbot messages.
/// </summary>
[ApiController]
[Route("chatbot/v1/messages")]
public class MessagesController : ControllerBase
{
    private readonly SendMessageCommandHandler _handler;
    private readonly ILogger<MessagesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesController"/> class.
    /// </summary>
    /// <param name="handler">The send message handler.</param>
    /// <param name="logger">The logger.</param>
    public MessagesController(
        SendMessageCommandHandler handler,
        ILogger<MessagesController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message in a chatbot session.
    /// </summary>
    /// <param name="request">The message request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI assistant's response.</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new SendMessageCommand
            {
                SessionId = request.SessionId,
                Content = request.Content,
                Attachments = request.Attachments?.Select(a => new AttachmentDto
                {
                    ContentType = a.Type?.ToLowerInvariant() switch
                    {
                        "image" => ContentType.Image,
                        "pdf" => ContentType.PDF,
                        "video" => ContentType.Video,
                        "audio" => ContentType.Audio,
                        _ => ContentType.Image
                    },
                    Data = a.Url,
                    MimeType = a.MimeType ?? string.Empty,
                    SizeBytes = a.SizeBytes ?? 0
                }).ToList()
            };

            var result = await _handler.HandleAsync(command, cancellationToken);

            var response = new MessageResponse
            {
                MessageId = result.MessageId,
                Content = result.Content,
                Role = result.Role == MessageRole.Assistant ? "assistant" : "user",
                Language = result.Language == Language.Thai ? "th" : "en",
                SuggestedActions = result.SuggestedActions.Select(sa => new SuggestedAction
                {
                    Text = sa.Text,
                    Label = sa.Text,
                    Action = sa.Type,
                    Data = sa.Data
                }).ToList(),
                CreatedAt = result.CreatedAt
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit exceeded"))
        {
            _logger.LogWarning(ex, "Rate limit exceeded for session {SessionId}", request.SessionId);

            // Calculate retry-after time (1 hour from now)
            var retryAfterSeconds = 3600;
            Response.Headers.Append("Retry-After", retryAfterSeconds.ToString());
            Response.Headers.Append("X-RateLimit-Limit", "100");
            Response.Headers.Append("X-RateLimit-Remaining", "0");

            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for session {SessionId}", request.SessionId);
            return BadRequest(new { error = ex.Message });
        }
    }
}
