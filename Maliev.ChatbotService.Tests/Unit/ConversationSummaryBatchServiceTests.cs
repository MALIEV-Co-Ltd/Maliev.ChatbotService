using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ConversationSummaryBatchServiceTests
{
    [Fact]
    public async Task SubmitExpiredSessionSummariesAsync_GeminiBatchAvailable_PersistsBatchAndReturnsDeferredSession()
    {
        var session = CreateSession();
        var messages = CreateMessages(session.Id);
        var summaryRepository = new Mock<IConversationSummaryRepository>();
        var sessionRepository = new Mock<IConversationSessionRepository>();
        var messageRepository = new Mock<IMessageRepository>();
        var batchClient = new Mock<IModelBatchClient>();
        var batchJobRepository = new Mock<IConversationSummaryBatchJobRepository>();
        ModelBatchRequest? capturedBatchRequest = null;
        ConversationSummaryBatchJob? capturedBatchJob = null;

        messageRepository
            .Setup(x => x.GetRecentBySessionIdAsync(session.Id, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);
        batchJobRepository
            .Setup(x => x.HasOpenItemForSessionAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        batchClient
            .Setup(x => x.CreateInlineGenerateContentBatchAsync(It.IsAny<ModelBatchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ModelBatchRequest, CancellationToken>((request, _) => capturedBatchRequest = request)
            .ReturnsAsync(new ModelBatchJob
            {
                Name = "batches/summary-1",
                State = "JOB_STATE_PENDING"
            });
        batchJobRepository
            .Setup(x => x.CreateAsync(It.IsAny<ConversationSummaryBatchJob>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationSummaryBatchJob, CancellationToken>((job, _) => capturedBatchJob = job)
            .ReturnsAsync((ConversationSummaryBatchJob job, CancellationToken _) => job);

        var service = CreateService(
            summaryRepository,
            sessionRepository,
            messageRepository,
            batchClient,
            batchJobRepository);

        var deferredSessionIds = await service.SubmitExpiredSessionSummariesAsync([session]);

        Assert.Contains(session.Id, deferredSessionIds);
        Assert.NotNull(capturedBatchRequest);
        Assert.Equal("gemini-2.5-flash-lite", capturedBatchRequest!.ModelName);
        Assert.StartsWith("expired-session-summaries-", capturedBatchRequest.DisplayName);
        var request = Assert.Single(capturedBatchRequest.Requests);
        Assert.Equal(session.Id.ToString(), request.Metadata["sessionId"]?.ToString());
        Assert.Equal(session.UserProfileId.ToString(), request.Metadata["userProfileId"]?.ToString());
        Assert.Equal("application/json", request.Request.ResponseMimeType);
        Assert.NotNull(request.Request.ResponseSchema);
        Assert.Equal(0, request.Request.ThinkingBudget);
        Assert.Equal("flex", request.Request.ServiceTier);
        Assert.Equal(GeminiRequest.FlexInferenceTimeoutSeconds, request.Request.TimeoutSeconds);

        Assert.NotNull(capturedBatchJob);
        Assert.Equal("batches/summary-1", capturedBatchJob!.BatchName);
        Assert.Equal(ConversationSummaryBatchStatus.Submitted, capturedBatchJob.Status);
        var item = Assert.Single(capturedBatchJob.Items);
        Assert.Equal(session.Id, item.SessionId);
        Assert.Equal(session.UserProfileId, item.UserProfileId);
        Assert.Equal(ConversationSummaryBatchStatus.Submitted, item.Status);
    }

    [Fact]
    public async Task SubmitExpiredSessionSummariesAsync_BatchUnsupported_ReturnsNoDeferredSessions()
    {
        var session = CreateSession();
        var summaryRepository = new Mock<IConversationSummaryRepository>();
        var sessionRepository = new Mock<IConversationSessionRepository>();
        var messageRepository = new Mock<IMessageRepository>();
        var batchClient = new Mock<IModelBatchClient>();
        var batchJobRepository = new Mock<IConversationSummaryBatchJobRepository>();

        messageRepository
            .Setup(x => x.GetRecentBySessionIdAsync(session.Id, 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMessages(session.Id));
        batchJobRepository
            .Setup(x => x.HasOpenItemForSessionAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        batchClient
            .Setup(x => x.CreateInlineGenerateContentBatchAsync(It.IsAny<ModelBatchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("Batch unsupported."));

        var service = CreateService(
            summaryRepository,
            sessionRepository,
            messageRepository,
            batchClient,
            batchJobRepository);

        var deferredSessionIds = await service.SubmitExpiredSessionSummariesAsync([session]);

        Assert.Empty(deferredSessionIds);
        batchJobRepository.Verify(
            x => x.CreateAsync(It.IsAny<ConversationSummaryBatchJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitExpiredSessionSummariesAsync_AboveConfiguredLimit_SubmitsAllSessionsInChunks()
    {
        var firstSession = CreateSession();
        var secondSession = CreateSession();
        var summaryRepository = new Mock<IConversationSummaryRepository>();
        var sessionRepository = new Mock<IConversationSessionRepository>();
        var messageRepository = new Mock<IMessageRepository>();
        var batchClient = new Mock<IModelBatchClient>();
        var batchJobRepository = new Mock<IConversationSummaryBatchJobRepository>();
        var batchCalls = 0;
        var createdJobs = new List<ConversationSummaryBatchJob>();

        foreach (var session in new[] { firstSession, secondSession })
        {
            messageRepository
                .Setup(x => x.GetRecentBySessionIdAsync(session.Id, 1000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateMessages(session.Id));
            batchJobRepository
                .Setup(x => x.HasOpenItemForSessionAsync(session.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        }

        batchClient
            .Setup(x => x.CreateInlineGenerateContentBatchAsync(It.IsAny<ModelBatchRequest>(), It.IsAny<CancellationToken>()))
            .Returns<ModelBatchRequest, CancellationToken>((_, _) =>
            {
                batchCalls++;
                return Task.FromResult(new ModelBatchJob
                {
                    Name = $"batches/summary-{batchCalls}",
                    State = "JOB_STATE_PENDING"
                });
            });
        batchJobRepository
            .Setup(x => x.CreateAsync(It.IsAny<ConversationSummaryBatchJob>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationSummaryBatchJob, CancellationToken>((job, _) => createdJobs.Add(job))
            .ReturnsAsync((ConversationSummaryBatchJob job, CancellationToken _) => job);

        var service = CreateService(
            summaryRepository,
            sessionRepository,
            messageRepository,
            batchClient,
            batchJobRepository,
            batchSummaryMaxSessions: "1");

        var deferredSessionIds = await service.SubmitExpiredSessionSummariesAsync([firstSession, secondSession]);

        Assert.Equal(2, batchCalls);
        Assert.Equal(2, createdJobs.Count);
        Assert.All(createdJobs, job => Assert.Single(job.Items));
        Assert.Contains(firstSession.Id, deferredSessionIds);
        Assert.Contains(secondSession.Id, deferredSessionIds);
    }

    [Fact]
    public async Task ProcessOpenBatchesAsync_SucceededInlineResponse_CreatesSummaryAndMarksItemSucceeded()
    {
        var session = CreateSession();
        var batchItem = new ConversationSummaryBatchItem
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UserProfileId = session.UserProfileId,
            Status = ConversationSummaryBatchStatus.Submitted,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
        var batchJob = new ConversationSummaryBatchJob
        {
            Id = Guid.NewGuid(),
            BatchName = "batches/summary-1",
            Provider = "gemini",
            ModelName = "gemini-2.5-flash-lite",
            DisplayName = "expired-session-summaries-20260701",
            Status = ConversationSummaryBatchStatus.Submitted,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            Items = [batchItem]
        };
        var summaryRepository = new Mock<IConversationSummaryRepository>();
        var sessionRepository = new Mock<IConversationSessionRepository>();
        var messageRepository = new Mock<IMessageRepository>();
        var batchClient = new Mock<IModelBatchClient>();
        var batchJobRepository = new Mock<IConversationSummaryBatchJobRepository>();
        ConversationSummary? capturedSummary = null;
        ConversationSession? updatedSession = null;
        ConversationSummaryBatchJob? updatedBatchJob = null;

        batchJobRepository
            .Setup(x => x.GetOpenJobsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([batchJob]);
        batchClient
            .Setup(x => x.GetBatchAsync("batches/summary-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelBatchJob
            {
                Name = "batches/summary-1",
                Done = true,
                State = "JOB_STATE_SUCCEEDED",
                InlineResponses =
                [
                    new ModelBatchInlineResponse
                    {
                        Metadata = new Dictionary<string, object?>
                        {
                            ["sessionId"] = session.Id.ToString(),
                            ["userProfileId"] = session.UserProfileId.ToString()
                        },
                        Response = new GeminiResponse
                        {
                            Success = true,
                            Content = """{"topics":["quote"],"decisions":[],"preferences":[],"entities":[],"intentCategories":["quotation_request"],"unresolvedQuestions":[]}""",
                            TokenUsage = new GeminiTokenUsage
                            {
                                PromptTokens = 10,
                                CompletionTokens = 5,
                                TotalTokens = 15
                            }
                        }
                    }
                ]
            });
        sessionRepository
            .Setup(x => x.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        summaryRepository
            .Setup(x => x.CreateAsync(It.IsAny<ConversationSummary>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationSummary, CancellationToken>((summary, _) => capturedSummary = summary)
            .ReturnsAsync((ConversationSummary summary, CancellationToken _) => summary);
        sessionRepository
            .Setup(x => x.UpdateAsync(It.IsAny<ConversationSession>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationSession, CancellationToken>((updated, _) => updatedSession = updated)
            .Returns(Task.CompletedTask);
        batchJobRepository
            .Setup(x => x.UpdateAsync(It.IsAny<ConversationSummaryBatchJob>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationSummaryBatchJob, CancellationToken>((updated, _) => updatedBatchJob = updated)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            summaryRepository,
            sessionRepository,
            messageRepository,
            batchClient,
            batchJobRepository);

        await service.ProcessOpenBatchesAsync();

        Assert.NotNull(capturedSummary);
        Assert.Equal(session.Id, capturedSummary!.SessionId);
        Assert.Contains("\"quote\"", capturedSummary.StructuredSummary);
        Assert.NotNull(updatedSession);
        Assert.Equal(SessionStatus.Closed, updatedSession!.Status);
        Assert.Equal(capturedSummary.Id, updatedSession.SummaryId);
        Assert.NotNull(updatedBatchJob);
        Assert.Equal(ConversationSummaryBatchStatus.Succeeded, updatedBatchJob!.Status);
        Assert.NotNull(updatedBatchJob.CompletedAt);
        Assert.Equal(ConversationSummaryBatchStatus.Succeeded, batchItem.Status);
        Assert.Contains("\"TotalTokens\":15", batchItem.TokenUsageJson);
    }

    private static ConversationSummaryBatchService CreateService(
        Mock<IConversationSummaryRepository> summaryRepository,
        Mock<IConversationSessionRepository> sessionRepository,
        Mock<IMessageRepository> messageRepository,
        Mock<IModelBatchClient> batchClient,
        Mock<IConversationSummaryBatchJobRepository> batchJobRepository,
        string batchSummaryMaxSessions = "20")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:IntentModelName"] = "gemini-2.5-flash-lite",
                ["Gemini:BatchSummaryMaxSessions"] = batchSummaryMaxSessions
            })
            .Build();

        return new ConversationSummaryBatchService(
            summaryRepository.Object,
            sessionRepository.Object,
            messageRepository.Object,
            batchClient.Object,
            batchJobRepository.Object,
            configuration,
            NullLogger<ConversationSummaryBatchService>.Instance);
    }

    private static ConversationSession CreateSession()
    {
        var now = DateTimeOffset.UtcNow;
        return new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            Channel = Channel.Website,
            Status = SessionStatus.Active,
            Language = Language.English,
            StartTime = now.AddDays(-1),
            LastActivityAt = now.AddHours(-25),
            ExpiresAt = now.AddHours(-1)
        };
    }

    private static List<Message> CreateMessages(Guid sessionId) =>
    [
        new Message
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "I need a quote for CNC parts.",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        },
        new Message
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.Assistant,
            Content = "I can help with that quote.",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9)
        }
    ];
}
