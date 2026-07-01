using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests durable persistence for Gemini conversation summary batch jobs.
/// </summary>
[Collection("Database")]
public class ConversationSummaryBatchJobRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationSummaryBatchJobRepositoryTests"/> class.
    /// </summary>
    /// <param name="factory">The integration test factory.</param>
    public ConversationSummaryBatchJobRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Verifies a submitted Gemini batch job and its per-session item can be restored by batch name.
    /// </summary>
    [Fact]
    public async Task CreateAsync_SubmittedJobWithItem_RoundTripsByBatchName()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConversationSummaryBatchJobRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        var (userId, sessionId) = await CreateSessionAsync(dbContext);

        var now = DateTimeOffset.UtcNow;
        var job = new ConversationSummaryBatchJob
        {
            Id = Guid.NewGuid(),
            BatchName = "batches/summary-123",
            DisplayName = "expired-session-summaries",
            Provider = "gemini",
            ModelName = "gemini-2.5-flash-lite",
            Status = ConversationSummaryBatchStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now,
            SubmittedAt = now,
            Items =
            [
                new ConversationSummaryBatchItem
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserProfileId = userId,
                    Status = ConversationSummaryBatchStatus.Submitted,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            ]
        };

        await repository.CreateAsync(job);

        var loaded = await repository.GetByBatchNameAsync("batches/summary-123");

        Assert.NotNull(loaded);
        Assert.Equal("expired-session-summaries", loaded.DisplayName);
        Assert.Equal(ConversationSummaryBatchStatus.Submitted, loaded.Status);
        var item = Assert.Single(loaded.Items);
        Assert.Equal(sessionId, item.SessionId);
        Assert.Equal(userId, item.UserProfileId);
        Assert.Equal(ConversationSummaryBatchStatus.Submitted, item.Status);
    }

    /// <summary>
    /// Verifies open item checks prevent duplicate batch submission for the same session.
    /// </summary>
    [Fact]
    public async Task HasOpenItemForSessionAsync_OpenAndTerminalItems_TracksOnlyOpenWork()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConversationSummaryBatchJobRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        var (userId, sessionId) = await CreateSessionAsync(dbContext);
        var now = DateTimeOffset.UtcNow;

        await repository.CreateAsync(new ConversationSummaryBatchJob
        {
            Id = Guid.NewGuid(),
            BatchName = "batches/open",
            DisplayName = "open",
            Provider = "gemini",
            ModelName = "gemini-2.5-flash-lite",
            Status = ConversationSummaryBatchStatus.Submitted,
            CreatedAt = now,
            UpdatedAt = now,
            Items =
            [
                new ConversationSummaryBatchItem
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserProfileId = userId,
                    Status = ConversationSummaryBatchStatus.Submitted,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            ]
        });

        Assert.True(await repository.HasOpenItemForSessionAsync(sessionId));

        var loaded = await repository.GetByBatchNameAsync("batches/open");
        Assert.NotNull(loaded);
        loaded.Status = ConversationSummaryBatchStatus.Succeeded;
        loaded.CompletedAt = now.AddMinutes(5);
        loaded.UpdatedAt = now.AddMinutes(5);
        loaded.Items[0].Status = ConversationSummaryBatchStatus.Succeeded;
        loaded.Items[0].CompletedAt = now.AddMinutes(5);
        loaded.Items[0].UpdatedAt = now.AddMinutes(5);
        await repository.UpdateAsync(loaded);

        Assert.False(await repository.HasOpenItemForSessionAsync(sessionId));
    }

    /// <summary>
    /// Verifies only non-terminal jobs are returned for polling.
    /// </summary>
    [Fact]
    public async Task GetOpenJobsAsync_ReturnsOnlyNonTerminalJobs()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConversationSummaryBatchJobRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        var (userId, sessionId) = await CreateSessionAsync(dbContext);
        var now = DateTimeOffset.UtcNow;

        await repository.CreateAsync(CreateJob("batches/pending", ConversationSummaryBatchStatus.Pending, userId, sessionId, now.AddMinutes(-2)));
        await repository.CreateAsync(CreateJob("batches/submitted", ConversationSummaryBatchStatus.Submitted, userId, sessionId, now.AddMinutes(-1)));
        await repository.CreateAsync(CreateJob("batches/succeeded", ConversationSummaryBatchStatus.Succeeded, userId, sessionId, now));

        var openJobs = await repository.GetOpenJobsAsync(limit: 10);

        Assert.Equal(["batches/pending", "batches/submitted"], openJobs.Select(job => job.BatchName).ToArray());
    }

    private static ConversationSummaryBatchJob CreateJob(
        string batchName,
        ConversationSummaryBatchStatus status,
        Guid userId,
        Guid sessionId,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            BatchName = batchName,
            DisplayName = batchName,
            Provider = "gemini",
            ModelName = "gemini-2.5-flash-lite",
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Items =
            [
                new ConversationSummaryBatchItem
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserProfileId = userId,
                    Status = status,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                }
            ]
        };

    private static async Task<(Guid UserId, Guid SessionId)> CreateSessionAsync(ChatbotDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        dbContext.UserProfiles.Add(new UserProfile
        {
            Id = userId,
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        });
        dbContext.ConversationSessions.Add(new ConversationSession
        {
            Id = sessionId,
            UserProfileId = userId,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow.AddHours(-25),
            LastActivityAt = DateTimeOffset.UtcNow.AddHours(-24),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            Language = Language.English,
            Status = SessionStatus.Active
        });
        await dbContext.SaveChangesAsync();
        return (userId, sessionId);
    }
}
