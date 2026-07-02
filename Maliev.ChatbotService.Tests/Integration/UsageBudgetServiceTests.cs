using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
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
    private const long DefaultCostBudgetMicroUsd = 5_000_000L;
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
        Assert.Equal(0, snapshot.UsedCostMicroUsd);
        Assert.Equal(DefaultCostBudgetMicroUsd, snapshot.DailyCostBudgetMicroUsd);
        Assert.Equal(DefaultCostBudgetMicroUsd, snapshot.RemainingCostMicroUsd);
        Assert.Equal(0, snapshot.CostUsedRatio);
        Assert.False(snapshot.IsCostExceeded);
        Assert.False(snapshot.IsExceeded);
    }

    [Fact]
    public async Task RecordModelUsage_AccumulatesTokenAndCostBudgets()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var userId = Guid.NewGuid();

        var first = await service.RecordModelUsageAsync(
            userId,
            new UsageBudgetCharge { Tokens = 100, CostMicroUsd = 250 });
        var second = await service.RecordModelUsageAsync(
            userId,
            new UsageBudgetCharge { Tokens = 50, CostMicroUsd = 125 });

        Assert.Equal(100, first.UsedTokens);
        Assert.Equal(250, first.UsedCostMicroUsd);
        Assert.Equal(150, second.UsedTokens);
        Assert.Equal(375, second.UsedCostMicroUsd);

        var snapshot = await service.GetDailyTokenUsageSnapshotAsync(userId);
        Assert.True(snapshot.IsEnabled);
        Assert.Equal(150, snapshot.UsedTokens);
        Assert.Equal(375, snapshot.UsedCostMicroUsd);
        Assert.Equal(DefaultCostBudgetMicroUsd - 375, snapshot.RemainingCostMicroUsd);
        Assert.Equal(375d / DefaultCostBudgetMicroUsd, snapshot.CostUsedRatio);
        Assert.False(snapshot.IsExceeded);
    }

    [Fact]
    public async Task RecordModelUsage_GoogleSearchGroundingUsesSharedDailyFreeAllowance()
    {
        await _factory.ClearRedisAsync();
        using var configuredFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("UsageBudget:DailyTokenBudget", "1000000");
            builder.UseSetting("UsageBudget:DailyCostBudgetMicroUsd", "5000000");
            builder.UseSetting("UsageBudget:GoogleSearchGroundingFreeDailyPromptAllowance", "2");
        });
        using var scope = configuredFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        var first = await service.RecordModelUsageAsync(
            firstUserId,
            new UsageBudgetCharge
            {
                Tokens = 10,
                CostMicroUsd = 100,
                GoogleSearchGroundingPromptCount = 2,
                GoogleSearchGroundingMicroUsd = 70000
            });
        var second = await service.RecordModelUsageAsync(
            secondUserId,
            new UsageBudgetCharge
            {
                Tokens = 10,
                CostMicroUsd = 100,
                GoogleSearchGroundingPromptCount = 1,
                GoogleSearchGroundingMicroUsd = 35000
            });

        Assert.Equal(100, first.UsedCostMicroUsd);
        Assert.Equal(2, first.FreeGoogleSearchGroundingPromptCount);
        Assert.Equal(0, first.BillableGoogleSearchGroundingPromptCount);
        Assert.Equal(0, first.ChargedGoogleSearchGroundingMicroUsd);
        Assert.Equal(35100, second.UsedCostMicroUsd);
        Assert.Equal(0, second.FreeGoogleSearchGroundingPromptCount);
        Assert.Equal(1, second.BillableGoogleSearchGroundingPromptCount);
        Assert.Equal(35000, second.ChargedGoogleSearchGroundingMicroUsd);
    }

    [Fact]
    public async Task RecordModelUsage_BeyondCostBudget_MarksExceeded()
    {
        using var configuredFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("UsageBudget:DailyTokenBudget", "1000000");
            builder.UseSetting("UsageBudget:DailyCostBudgetMicroUsd", "100");
        });
        using var scope = configuredFactory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUsageBudgetService>();
        var userId = Guid.NewGuid();

        var total = await service.RecordModelUsageAsync(
            userId,
            new UsageBudgetCharge { Tokens = 10, CostMicroUsd = 101 });

        Assert.Equal(10, total.UsedTokens);
        Assert.Equal(101, total.UsedCostMicroUsd);
        Assert.True(await service.IsDailyTokenBudgetExceededAsync(userId));
        var snapshot = await service.GetDailyTokenUsageSnapshotAsync(userId);
        Assert.False(snapshot.IsTokenExceeded);
        Assert.True(snapshot.IsCostExceeded);
        Assert.True(snapshot.IsExceeded);
        Assert.Equal(101, snapshot.UsedCostMicroUsd);
        Assert.Equal(0, snapshot.RemainingCostMicroUsd);
        Assert.Equal(1, snapshot.CostUsedRatio);
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
