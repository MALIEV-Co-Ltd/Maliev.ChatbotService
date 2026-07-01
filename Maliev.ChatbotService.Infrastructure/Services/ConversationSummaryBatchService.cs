using System.Text;
using System.Text.Json;
using Maliev.ChatbotService.Application.Costing;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for batching non-urgent conversation summary generation through a model provider.
/// </summary>
public class ConversationSummaryBatchService : IConversationSummaryBatchService
{
    private const int DefaultBatchSummaryMaxSessions = 20;
    private const int DefaultBatchSummaryMaxInlineBytes = 18 * 1024 * 1024;
    private const int OpenBatchPollLimit = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConversationSummaryRepository _summaryRepository;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IModelBatchClient _batchClient;
    private readonly IConversationSummaryBatchJobRepository _batchJobRepository;
    private readonly ILogger<ConversationSummaryBatchService> _logger;
    private readonly string _modelName;
    private readonly int _maxBatchSessions;
    private readonly int _maxBatchInlineBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationSummaryBatchService"/> class.
    /// </summary>
    /// <param name="summaryRepository">The conversation summary repository.</param>
    /// <param name="sessionRepository">The conversation session repository.</param>
    /// <param name="messageRepository">The message repository.</param>
    /// <param name="batchClient">The model batch client.</param>
    /// <param name="batchJobRepository">The batch job repository.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">The logger.</param>
    public ConversationSummaryBatchService(
        IConversationSummaryRepository summaryRepository,
        IConversationSessionRepository sessionRepository,
        IMessageRepository messageRepository,
        IModelBatchClient batchClient,
        IConversationSummaryBatchJobRepository batchJobRepository,
        IConfiguration configuration,
        ILogger<ConversationSummaryBatchService> logger)
    {
        _summaryRepository = summaryRepository;
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _batchClient = batchClient;
        _batchJobRepository = batchJobRepository;
        _logger = logger;
        _modelName = configuration["Gemini:IntentModelName"] ?? "gemini-2.5-flash-lite";
        _maxBatchSessions = int.TryParse(configuration["Gemini:BatchSummaryMaxSessions"], out var configuredLimit) &&
            configuredLimit > 0
            ? configuredLimit
            : DefaultBatchSummaryMaxSessions;
        _maxBatchInlineBytes = int.TryParse(configuration["Gemini:BatchSummaryMaxInlineBytes"], out var configuredInlineBytes) &&
            configuredInlineBytes > 0
            ? configuredInlineBytes
            : DefaultBatchSummaryMaxInlineBytes;
    }

    /// <inheritdoc/>
    public async Task<HashSet<Guid>> SubmitExpiredSessionSummariesAsync(
        IReadOnlyCollection<ConversationSession> sessions,
        CancellationToken cancellationToken = default)
    {
        var deferredSessionIds = new HashSet<Guid>();
        if (sessions.Count == 0)
        {
            return deferredSessionIds;
        }

        var candidates = new List<SummaryBatchCandidate>();
        foreach (var session in sessions)
        {
            if (session.SummaryId.HasValue)
            {
                continue;
            }

            if (await _batchJobRepository.HasOpenItemForSessionAsync(session.Id, cancellationToken))
            {
                deferredSessionIds.Add(session.Id);
                continue;
            }

            var messages = await _messageRepository.GetRecentBySessionIdAsync(
                session.Id,
                ConversationSummaryGeminiRequestFactory.MaxSummaryMessages,
                cancellationToken);
            var messageList = messages.OrderBy(message => message.CreatedAt).ToList();
            if (messageList.Count == 0)
            {
                continue;
            }

            var conversationText = ConversationSummaryGeminiRequestFactory.BuildConversationText(messageList);
            candidates.Add(new SummaryBatchCandidate(
                session,
                new ModelBatchGenerateContentRequest
                {
                    Request = ConversationSummaryGeminiRequestFactory.CreateRequest(conversationText, _modelName),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["sessionId"] = session.Id.ToString(),
                        ["userProfileId"] = session.UserProfileId.ToString()
                    }
                }));
        }

        if (candidates.Count == 0)
        {
            return deferredSessionIds;
        }

        foreach (var candidateBatch in ChunkCandidatesForInlineBatch(candidates))
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var batchRequest = new ModelBatchRequest
                {
                    DisplayName = $"expired-session-summaries-{now:yyyyMMddHHmmss}",
                    ModelName = _modelName,
                    Priority = -10,
                    Requests = candidateBatch.Select(candidate => candidate.Request).ToList()
                };

                var providerJob = await _batchClient.CreateInlineGenerateContentBatchAsync(batchRequest, cancellationToken);
                if (string.IsNullOrWhiteSpace(providerJob.Name))
                {
                    throw new InvalidOperationException("Gemini Batch API did not return a batch resource name.");
                }

                var batchJob = new ConversationSummaryBatchJob
                {
                    Id = Guid.NewGuid(),
                    BatchName = providerJob.Name,
                    Provider = "gemini",
                    ModelName = _modelName,
                    DisplayName = batchRequest.DisplayName,
                    Status = MapProviderStatus(providerJob),
                    CreatedAt = now,
                    UpdatedAt = now,
                    SubmittedAt = now,
                    Items = candidateBatch.Select(candidate => new ConversationSummaryBatchItem
                    {
                        Id = Guid.NewGuid(),
                        SessionId = candidate.Session.Id,
                        UserProfileId = candidate.Session.UserProfileId,
                        Status = ConversationSummaryBatchStatus.Submitted,
                        CreatedAt = now,
                        UpdatedAt = now
                    }).ToList()
                };

                await _batchJobRepository.CreateAsync(batchJob, cancellationToken);

                foreach (var candidate in candidateBatch)
                {
                    deferredSessionIds.Add(candidate.Session.Id);
                }

                _logger.LogInformation(
                    "Submitted {Count} expired sessions for Gemini batch summary generation as {BatchName}",
                    candidateBatch.Length,
                    providerJob.Name);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogDebug(ex, "Configured model provider does not support batch summaries; falling back to synchronous summaries");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit expired session summaries to Gemini Batch API");
            }
        }

        return deferredSessionIds;
    }

    private IEnumerable<SummaryBatchCandidate[]> ChunkCandidatesForInlineBatch(IReadOnlyList<SummaryBatchCandidate> candidates)
    {
        var batch = new List<SummaryBatchCandidate>();
        foreach (var candidate in candidates)
        {
            if (batch.Count > 0 &&
                (batch.Count >= _maxBatchSessions ||
                 EstimateInlineBatchBytes(batch, candidate) > _maxBatchInlineBytes))
            {
                yield return batch.ToArray();
                batch.Clear();
            }

            batch.Add(candidate);
        }

        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }

    private static int EstimateInlineBatchBytes(
        IReadOnlyCollection<SummaryBatchCandidate> currentBatch,
        SummaryBatchCandidate candidate)
    {
        var requests = currentBatch
            .Select(item => item.Request)
            .Append(candidate.Request)
            .ToList();
        return EstimateInlineBatchBytes(requests);
    }

    private static int EstimateInlineBatchBytes(IReadOnlyCollection<ModelBatchGenerateContentRequest> requests)
    {
        var payload = new Dictionary<string, object?>
        {
            ["batch"] = new Dictionary<string, object?>
            {
                ["display_name"] = "expired-session-summaries-00000000000000",
                ["input_config"] = new Dictionary<string, object?>
                {
                    ["requests"] = new Dictionary<string, object?>
                    {
                        ["requests"] = requests.Select(item => new Dictionary<string, object?>
                        {
                            ["request"] = GeminiClient.BuildGeminiPayload(item.Request),
                            ["metadata"] = item.Metadata
                        }).ToArray()
                    }
                },
                ["priority"] = "-10"
            }
        };

        return Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(payload, JsonOptions));
    }

    /// <inheritdoc/>
    public async Task ProcessOpenBatchesAsync(CancellationToken cancellationToken = default)
    {
        var openJobs = await _batchJobRepository.GetOpenJobsAsync(OpenBatchPollLimit, cancellationToken);
        if (openJobs.Count == 0)
        {
            return;
        }

        foreach (var job in openJobs)
        {
            try
            {
                var providerJob = await _batchClient.GetBatchAsync(job.BatchName, cancellationToken);
                var now = DateTimeOffset.UtcNow;
                job.Status = MapProviderStatus(providerJob);
                job.UpdatedAt = now;

                if (job.Status == ConversationSummaryBatchStatus.Succeeded)
                {
                    await ApplyInlineResponsesAsync(job, providerJob.InlineResponses, now, cancellationToken);
                }
                else if (IsTerminalStatus(job.Status))
                {
                    await MarkOpenItemsFailedAsync(
                        job,
                        providerJob.State ?? "Batch job reached a terminal failure state.",
                        now,
                        cancellationToken);
                }

                if (IsTerminalStatus(job.Status))
                {
                    job.CompletedAt ??= now;
                }

                await _batchJobRepository.UpdateAsync(job, cancellationToken);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogDebug(ex, "Configured model provider does not support batch polling");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Gemini batch summary job {BatchName}", job.BatchName);
            }
        }
    }

    private async Task ApplyInlineResponsesAsync(
        ConversationSummaryBatchJob job,
        List<ModelBatchInlineResponse> responses,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var completedItemIds = new HashSet<Guid>();
        foreach (var response in responses)
        {
            if (!TryGetMetadataGuid(response.Metadata, "sessionId", out var sessionId))
            {
                _logger.LogWarning("Gemini batch summary response missing sessionId metadata for batch {BatchName}", job.BatchName);
                continue;
            }

            var item = job.Items.FirstOrDefault(candidate => candidate.SessionId == sessionId);
            if (item is null)
            {
                _logger.LogWarning(
                    "Gemini batch summary response referenced unknown session {SessionId} for batch {BatchName}",
                    sessionId,
                    job.BatchName);
                continue;
            }

            if (IsTerminalStatus(item.Status))
            {
                completedItemIds.Add(item.Id);
                continue;
            }

            completedItemIds.Add(item.Id);
            if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
            {
                await CompleteItemAsync(
                    item,
                    ConversationSummaryBatchStatus.Failed,
                    ConversationSummaryGeminiRequestFactory.EmptySummaryJson,
                    response.ErrorMessage,
                    null,
                    job.ModelName,
                    now,
                    cancellationToken);
                continue;
            }

            var summaryJson = response.Response is null
                ? ConversationSummaryGeminiRequestFactory.EmptySummaryJson
                : ConversationSummaryGeminiRequestFactory.CleanJsonResponse(response.Response.Content);

            if (response.Response?.Success != true ||
                !ConversationSummaryGeminiRequestFactory.IsValidSummaryJson(summaryJson))
            {
                await CompleteItemAsync(
                    item,
                    ConversationSummaryBatchStatus.Failed,
                    ConversationSummaryGeminiRequestFactory.EmptySummaryJson,
                    response.Response?.ErrorMessage ?? "Gemini batch summary response was invalid.",
                    response.Response?.TokenUsage,
                    job.ModelName,
                    now,
                    cancellationToken);
                continue;
            }

            await CompleteItemAsync(
                item,
                ConversationSummaryBatchStatus.Succeeded,
                summaryJson,
                null,
                response.Response.TokenUsage,
                job.ModelName,
                now,
                cancellationToken);
        }

        foreach (var item in job.Items.Where(item => !IsTerminalStatus(item.Status) && !completedItemIds.Contains(item.Id)))
        {
            await CompleteItemAsync(
                item,
                ConversationSummaryBatchStatus.Failed,
                ConversationSummaryGeminiRequestFactory.EmptySummaryJson,
                "Gemini batch summary response did not include this session.",
                null,
                job.ModelName,
                now,
                cancellationToken);
        }
    }

    private async Task MarkOpenItemsFailedAsync(
        ConversationSummaryBatchJob job,
        string errorMessage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var item in job.Items.Where(item => !IsTerminalStatus(item.Status)))
        {
            await CompleteItemAsync(
                item,
                ConversationSummaryBatchStatus.Failed,
                ConversationSummaryGeminiRequestFactory.EmptySummaryJson,
                errorMessage,
                null,
                job.ModelName,
                now,
                cancellationToken);
        }
    }

    private async Task CompleteItemAsync(
        ConversationSummaryBatchItem item,
        ConversationSummaryBatchStatus status,
        string summaryJson,
        string? errorMessage,
        GeminiTokenUsage? tokenUsage,
        string modelName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tokenUsageJson = tokenUsage is null ? null : JsonSerializer.Serialize(tokenUsage);
        var costEstimate = GeminiCostEstimator.Estimate(modelName, "batch", tokenUsage);
        var costEstimateJson = costEstimate is null ? null : JsonSerializer.Serialize(costEstimate);

        var session = await _sessionRepository.GetByIdAsync(item.SessionId, cancellationToken);
        if (session is null)
        {
            item.Status = ConversationSummaryBatchStatus.Failed;
            item.ErrorMessage = $"Session {item.SessionId} was not found.";
            item.TokenUsageJson = tokenUsageJson;
            item.CostEstimateJson = costEstimateJson;
            item.UpdatedAt = now;
            item.CompletedAt = now;
            return;
        }

        if (!session.SummaryId.HasValue)
        {
            var summary = new ConversationSummary
            {
                Id = Guid.NewGuid(),
                SessionId = item.SessionId,
                UserProfileId = item.UserProfileId,
                StructuredSummary = summaryJson,
                CreatedAt = now
            };

            var createdSummary = await _summaryRepository.CreateAsync(summary, cancellationToken);
            session.SummaryId = createdSummary.Id;
        }

        session.Status = SessionStatus.Closed;
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        item.Status = status;
        item.StructuredSummary = summaryJson;
        item.ErrorMessage = errorMessage;
        item.TokenUsageJson = tokenUsageJson;
        item.CostEstimateJson = costEstimateJson;
        item.UpdatedAt = now;
        item.CompletedAt = now;
    }

    private static ConversationSummaryBatchStatus MapProviderStatus(ModelBatchJob providerJob)
    {
        return providerJob.State switch
        {
            "JOB_STATE_SUCCEEDED" => ConversationSummaryBatchStatus.Succeeded,
            "JOB_STATE_FAILED" => ConversationSummaryBatchStatus.Failed,
            "JOB_STATE_CANCELLED" => ConversationSummaryBatchStatus.Cancelled,
            "JOB_STATE_EXPIRED" => ConversationSummaryBatchStatus.Expired,
            _ when providerJob.Done && providerJob.InlineResponses.Count > 0 => ConversationSummaryBatchStatus.Succeeded,
            _ => ConversationSummaryBatchStatus.Submitted
        };
    }

    private static bool IsTerminalStatus(ConversationSummaryBatchStatus status) =>
        status is ConversationSummaryBatchStatus.Succeeded
            or ConversationSummaryBatchStatus.Failed
            or ConversationSummaryBatchStatus.Cancelled
            or ConversationSummaryBatchStatus.Expired;

    private static bool TryGetMetadataGuid(Dictionary<string, object?> metadata, string key, out Guid value)
    {
        value = default;
        return metadata.TryGetValue(key, out var rawValue) &&
            Guid.TryParse(rawValue?.ToString(), out value);
    }

    private sealed record SummaryBatchCandidate(
        ConversationSession Session,
        ModelBatchGenerateContentRequest Request);
}
