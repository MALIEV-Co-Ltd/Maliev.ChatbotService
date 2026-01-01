using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service implementation for conversation summary operations.
/// </summary>
public class ConversationSummaryService : IConversationSummaryService
{
    private readonly IConversationSummaryRepository _summaryRepository;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<ConversationSummaryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationSummaryService"/> class.
    /// </summary>
    /// <param name="summaryRepository">The conversation summary repository.</param>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="messageRepository">The message repository.</param>
    /// <param name="geminiClient">The Gemini API client.</param>
    /// <param name="logger">The logger.</param>
    public ConversationSummaryService(
        IConversationSummaryRepository summaryRepository,
        IConversationSessionRepository sessionRepository,
        IMessageRepository messageRepository,
        IGeminiClient geminiClient,
        ILogger<ConversationSummaryService> logger)
    {
        _summaryRepository = summaryRepository;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _geminiClient = geminiClient;
        _logger = logger;
    }

    /// <summary>
    /// Generates a structured summary for a conversation session.
    /// </summary>
    /// <param name="sessionId">The session ID to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created conversation summary.</returns>
    public async Task<ConversationSummary> GenerateSummaryAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // Get all messages in the session
        var messages = await _messageRepository.GetRecentBySessionIdAsync(sessionId, 1000, cancellationToken);
        var messageList = messages.OrderBy(m => m.CreatedAt).ToList();

        if (messageList.Count == 0)
        {
            _logger.LogWarning("No messages found for session {SessionId}, creating empty summary", sessionId);
            return await CreateEmptySummaryAsync(session, cancellationToken);
        }

        // Build conversation text for summarization
        var conversationText = string.Join("\n", messageList.Select(m =>
            $"{(m.Role == MessageRole.User ? "User" : "Assistant")}: {m.Content}"));

        // Create summarization prompt
        var systemInstruction = @"You are a conversation summarization assistant. Your task is to analyze a conversation and extract structured information in JSON format.

Extract the following:
1. topics: Array of main topics discussed (e.g., [""CNC machining"", ""material selection""])
2. decisions: Array of decisions made (e.g., [""Customer prefers aluminum"", ""Needs quotation for 100 units""])
3. preferences: Array of user preferences (e.g., [""Prefers 6061 aluminum"", ""Requires anodized finish""])
4. entities: Array of important entities mentioned (e.g., [""100 units"", ""6061 aluminum"", ""delivery by March 15""])
5. intentCategories: Array of intent categories (e.g., [""quotation_request"", ""technical_inquiry"", ""order_status""])
6. unresolvedQuestions: Array of questions that were not answered (e.g., [""What is the lead time?"", ""Can you provide samples?""])

Respond with ONLY valid JSON. No markdown, no code blocks, just the JSON object.";

        var geminiRequest = new GeminiRequest
        {
            SystemInstruction = systemInstruction,
            Messages = new List<GeminiMessage>
            {
                new GeminiMessage
                {
                    Role = "user",
                    Content = $"Summarize this conversation:\n\n{conversationText}"
                }
            },
            TimeoutSeconds = 30
        };

        var response = await _geminiClient.SendMessageAsync(geminiRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("Failed to generate summary for session {SessionId}: {Error}", sessionId, response.ErrorMessage);
            return await CreateEmptySummaryAsync(session, cancellationToken);
        }

        // Clean the response to ensure it's valid JSON
        var summaryJson = CleanJsonResponse(response.Content);

        // Validate JSON structure
        if (!IsValidSummaryJson(summaryJson))
        {
            _logger.LogWarning("Invalid summary JSON for session {SessionId}, creating empty summary", sessionId);
            return await CreateEmptySummaryAsync(session, cancellationToken);
        }

        var summary = new ConversationSummary
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserProfileId = session.UserProfileId,
            StructuredSummary = summaryJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createdSummary = await _summaryRepository.CreateAsync(summary, cancellationToken);

        // Update session with summary ID
        session.SummaryId = createdSummary.Id;
        session.Status = SessionStatus.Closed;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        _logger.LogInformation("Generated summary {SummaryId} for session {SessionId}", createdSummary.Id, sessionId);

        return createdSummary;
    }

    /// <summary>
    /// Retrieves recent conversation summaries for a user profile.
    /// </summary>
    /// <param name="userProfileId">The user profile ID.</param>
    /// <param name="limit">The maximum number of summaries to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection of conversation summaries.</returns>
    public async Task<IEnumerable<ConversationSummary>> GetRecentSummariesAsync(Guid userProfileId, int limit = 3, CancellationToken cancellationToken = default)
    {
        return await _summaryRepository.GetRecentByUserProfileIdAsync(userProfileId, limit, cancellationToken);
    }

    private async Task<ConversationSummary> CreateEmptySummaryAsync(ConversationSession session, CancellationToken cancellationToken)
    {
        var emptySummary = new ConversationSummary
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UserProfileId = session.UserProfileId,
            StructuredSummary = "{\"topics\":[],\"decisions\":[],\"preferences\":[],\"entities\":[],\"intentCategories\":[],\"unresolvedQuestions\":[]}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createdSummary = await _summaryRepository.CreateAsync(emptySummary, cancellationToken);

        session.SummaryId = createdSummary.Id;
        session.Status = SessionStatus.Closed;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        return createdSummary;
    }

    private static string CleanJsonResponse(string content)
    {
        // Remove markdown code blocks if present
        var cleaned = content.Trim();
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned.Substring(7);
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Substring(3);
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        }

        return cleaned.Trim();
    }

    private static bool IsValidSummaryJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Validate required fields exist
            return root.TryGetProperty("topics", out _) &&
                   root.TryGetProperty("decisions", out _) &&
                   root.TryGetProperty("preferences", out _) &&
                   root.TryGetProperty("entities", out _) &&
                   root.TryGetProperty("intentCategories", out _) &&
                   root.TryGetProperty("unresolvedQuestions", out _);
        }
        catch
        {
            return false;
        }
    }
}
