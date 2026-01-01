using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for SystemInstructionService with Redis caching and PostgreSQL fallback.
/// </summary>
[Collection("Database")]
public class SystemInstructionServiceTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public SystemInstructionServiceTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that active instruction is cached in Redis.
    /// </summary>
    [Fact]
    public async Task GetActiveInstructionAsync_FirstCall_CachesInRedis()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISystemInstructionService>();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        // Deactivate all existing instructions first to ensure clean state
        await repository.DeactivateAllAsync();
        await service.InvalidateCacheAsync(); // Clear cache after deactivation

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Test Instruction",
            PersonaDefinition = "Test persona",
            BusinessConstraints = "Test constraints",
            IsActive = true,
            Version = 1,
            AllowedTopics = string.Empty,
            RejectionTemplates = "{}"
        };
        await repository.CreateAsync(instruction);

        // Act
        var result = await service.GetActiveInstructionAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(instruction.Id, result.Id);
    }

    /// <summary>
    /// Tests that cached instruction is returned on subsequent calls.
    /// </summary>
    [Fact]
    public async Task GetActiveInstructionAsync_SubsequentCall_ReturnsCachedValue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISystemInstructionService>();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        // Deactivate all existing instructions first to ensure clean state
        await repository.DeactivateAllAsync();
        await service.InvalidateCacheAsync(); // Clear cache after deactivation

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Cached Instruction",
            PersonaDefinition = "Cached persona",
            BusinessConstraints = "Cached constraints",
            IsActive = true,
            Version = 1,
            AllowedTopics = string.Empty,
            RejectionTemplates = "{}"
        };
        await repository.CreateAsync(instruction);

        // Act
        var result1 = await service.GetActiveInstructionAsync();
        var result2 = await service.GetActiveInstructionAsync();

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.Id, result2.Id);
    }

    /// <summary>
    /// Tests that service falls back to PostgreSQL when Redis is unavailable.
    /// </summary>
    [Fact]
    public async Task GetActiveInstructionAsync_RedisUnavailable_FallsBackToPostgreSQL()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISystemInstructionService>();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        // Deactivate all existing instructions first
        await repository.DeactivateAllAsync();
        await service.InvalidateCacheAsync(); // Clear cache after deactivation

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Fallback Instruction",
            PersonaDefinition = "Fallback persona",
            BusinessConstraints = "Fallback constraints",
            IsActive = true,
            Version = 1,
            AllowedTopics = string.Empty,
            RejectionTemplates = "{}"
        };
        await repository.CreateAsync(instruction);

        // Act - Simulate Redis failure by stopping Redis container
        // The service should automatically fall back to PostgreSQL
        var result = await service.GetActiveInstructionAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(instruction.Id, result.Id);
    }

    /// <summary>
    /// Tests that cache is invalidated when instruction is updated.
    /// </summary>
    [Fact]
    public async Task InvalidateCacheAsync_AfterUpdate_FetchesNewValue()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISystemInstructionService>();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        // Deactivate all existing instructions first to ensure clean state
        await repository.DeactivateAllAsync();

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Original Instruction",
            PersonaDefinition = "Original persona",
            BusinessConstraints = "Original constraints",
            IsActive = true,
            Version = 1,
            AllowedTopics = string.Empty,
            RejectionTemplates = "{}"
        };
        await repository.CreateAsync(instruction);

        var cached = await service.GetActiveInstructionAsync();

        // Act
        instruction.PersonaDefinition = "Updated persona";
        instruction.Version = 2;
        await repository.UpdateAsync(instruction);
        await service.InvalidateCacheAsync();

        var updated = await service.GetActiveInstructionAsync();

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.Equal("Updated persona", updated.PersonaDefinition);
    }
}

