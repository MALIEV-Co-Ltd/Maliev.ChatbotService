using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for OperationLog repository CRUD operations and operation history queries.
/// </summary>
[Collection("Database")]
public class OperationLogRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public OperationLogRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Initializes the test.
    /// </summary>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
    /// <summary>
    /// Tests that a valid operation log can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidOperationLog_ReturnsCreatedLog()
    {
        using var scope = _factory.Services.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<IOperationLogRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.InternalAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var log = new OperationLog
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            OperationType = "QuotationQuery",
            OperationParameters = "{\"quotationId\":\"Q-2025-1234\"}",
            ExecutionResult = "{\"status\":\"Pending\",\"customer\":\"ABC Corp\"}",
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = true
        };

        var created = await logRepo.CreateAsync(log);

        Assert.NotNull(created);
        Assert.Equal("QuotationQuery", created.OperationType);
        Assert.True(created.Success);
    }

    /// <summary>
    /// Tests that multiple operation logs for a user are returned.
    /// </summary>
    [Fact]
    public async Task GetLogsByUserIdAsync_MultipleOperations_ReturnsAllLogs()
    {
        using var scope = _factory.Services.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<IOperationLogRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.InternalAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var log1 = new OperationLog
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            OperationType = "OrderQuery",
            OperationParameters = "{\"orderId\":\"O-123\"}",
            ExecutionResult = "{\"status\":\"Shipped\"}",
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = true
        };

        var log2 = new OperationLog
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            OperationType = "CRMUpdate",
            OperationParameters = "{\"customerId\":\"C-456\"}",
            ExecutionResult = "{\"updated\":true}",
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = true
        };

        await logRepo.CreateAsync(log1);
        await logRepo.CreateAsync(log2);

        var logs = await logRepo.GetLogsByUserIdAsync(userProfile.Id);

        Assert.Equal(2, logs.Count());
    }

    /// <summary>
    /// Tests that operation logs can be filtered by operation type.
    /// </summary>
    [Fact]
    public async Task GetLogsByOperationTypeAsync_SpecificType_ReturnsMatchingLogs()
    {
        using var scope = _factory.Services.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<IOperationLogRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.InternalAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var log = new OperationLog
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            OperationType = "SendReminder",
            OperationParameters = "{\"quotationId\":\"Q-789\"}",
            ExecutionResult = "{\"sent\":true}",
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = true
        };

        await logRepo.CreateAsync(log);

        var logs = await logRepo.GetLogsByOperationTypeAsync("SendReminder");

        Assert.Single(logs);
        Assert.Equal("SendReminder", logs.First().OperationType);
    }

    /// <summary>
    /// Tests that failed operations are recorded with success = false.
    /// </summary>
    [Fact]
    public async Task CreateAsync_FailedOperation_RecordsFailure()
    {
        using var scope = _factory.Services.CreateScope();
        var logRepo = scope.ServiceProvider.GetRequiredService<IOperationLogRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.InternalAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var log = new OperationLog
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            OperationType = "QuotationQuery",
            OperationParameters = "{\"quotationId\":\"Q-INVALID\"}",
            ExecutionResult = "{\"error\":\"Quotation not found\"}",
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = false
        };

        var created = await logRepo.CreateAsync(log);

        Assert.False(created.Success);
        Assert.Contains("error", created.ExecutionResult);
    }
}

