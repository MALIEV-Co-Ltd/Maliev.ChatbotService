using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for <see cref="IUsageBudgetService"/> — the Redis-backed rolling daily token budget (S2).
/// Uses the default budget (2,000,000 tokens) and fresh user IDs per test so windows never collide.
/// </summary>
[Collection("Database")]
public class UsageBudgetServiceTests : IAsyncLifetime
{
    private const long DefaultBudget = 2_000_000L;
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public UsageBudgetServiceTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IsDailyTokenBudgetExceeded_NoUsage_ReturnsFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var userId = Guid.NewGuid();

        Assert.False(await service.IsDailyTokenBudgetExceededAsync(userId));
        Assert.Equal(0, await service.GetDailyTokenUsageAsync(userId));
    }

    [Fact]
    public async Task RecordTokenUsage_AccumulatesAndReportsRunningTotal()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var userId = Guid.NewGuid();

        Assert.Equal(100, await service.RecordTokenUsageAsync(userId, 100));
        Assert.Equal(350, await service.RecordTokenUsageAsync(userId, 250));
        Assert.Equal(350, await service.GetDailyTokenUsageAsync(userId));
        Assert.False(await service.IsDailyTokenBudgetExceededAsync(userId));
        var snapshot = await service.GetDailyTokenUsageSnapshotAsync(userId);
        Assert.True(snapshot.IsEnabled);
        Assert.Equal(350, snapshot.UsedTokens);
        Assert.Equal(DefaultBudget, snapshot.DailyTokenBudget);
        Assert.Equal(DefaultBudget - 350, snapshot.RemainingTokens);
        Assert.Equal(350d / DefaultBudget, snapshot.UsedRatio);
        Assert.False(snapshot.IsExceeded);
    }

    [Fact]
    public async Task RecordTokenUsage_BeyondBudget_MarksExceeded()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var userId = Guid.NewGuid();

        var total = await service.RecordTokenUsageAsync(userId, DefaultBudget + 1);

        Assert.Equal(DefaultBudget + 1, total);
        Assert.True(await service.IsDailyTokenBudgetExceededAsync(userId));
        var snapshot = await service.GetDailyTokenUsageSnapshotAsync(userId);
        Assert.True(snapshot.IsExceeded);
        Assert.Equal(DefaultBudget + 1, snapshot.UsedTokens);
        Assert.Equal(0, snapshot.RemainingTokens);
        Assert.Equal(1, snapshot.UsedRatio);
    }

    [Fact]
    public async Task RecordTokenUsage_NonPositiveAmount_IsNoOp()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var userId = Guid.NewGuid();

        await service.RecordTokenUsageAsync(userId, 100);
        Assert.Equal(100, await service.RecordTokenUsageAsync(userId, 0));
        Assert.Equal(100, await service.RecordTokenUsageAsync(userId, -50));

        Assert.Equal(100, await service.GetDailyTokenUsageAsync(userId));
    }

    [Fact]
    public async Task RecordTokenUsage_DifferentUsers_TrackedSeparately()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await service.RecordTokenUsageAsync(user1, DefaultBudget + 1);

        Assert.True(await service.IsDailyTokenBudgetExceededAsync(user1));
        Assert.False(await service.IsDailyTokenBudgetExceededAsync(user2));
        Assert.Equal(0, await service.GetDailyTokenUsageAsync(user2));
    }
}
