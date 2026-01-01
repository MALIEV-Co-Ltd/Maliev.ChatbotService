using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for SystemInstruction repository CRUD operations and active instruction retrieval.
/// </summary>
[Collection("Database")]
public class SystemInstructionRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public SystemInstructionRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that a valid system instruction can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidInstruction_ReturnsCreatedInstruction()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Manufacturing Context",
            PersonaDefinition = "You are a helpful manufacturing assistant",
            BusinessConstraints = "Only discuss manufacturing topics",
            IsActive = true,
            Version = 1
        };

        var created = await repo.CreateAsync(instruction);

        Assert.NotNull(created);
        Assert.Equal("Manufacturing Context", created.Name);
        Assert.True(created.IsActive);
    }

    /// <summary>
    /// Tests that the active system instruction can be retrieved.
    /// </summary>
    [Fact]
    public async Task GetActiveInstructionAsync_ActiveInstruction_ReturnsInstruction()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Active Instruction",
            PersonaDefinition = "Active persona",
            BusinessConstraints = "Active constraints",
            IsActive = true,
            Version = 1
        };

        await repo.CreateAsync(instruction);
        var active = await repo.GetActiveInstructionAsync();

        Assert.NotNull(active);
        Assert.True(active.IsActive);
    }

    /// <summary>
    /// Tests that a system instruction can be deactivated successfully.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_DeactivateInstruction_UpdatesSuccessfully()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Test Instruction",
            PersonaDefinition = "Test persona",
            BusinessConstraints = "Test constraints",
            IsActive = true,
            Version = 1
        };

        await repo.CreateAsync(instruction);
        instruction.IsActive = false;
        instruction.Version = 2;
        await repo.UpdateAsync(instruction);

        var updated = await repo.GetByIdAsync(instruction.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.Equal(2, updated.Version);
    }

    /// <summary>
    /// Tests that a system instruction can be retrieved by version number.
    /// </summary>
    [Fact]
    public async Task GetByVersionAsync_SpecificVersion_ReturnsInstruction()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Versioned Instruction",
            PersonaDefinition = "Version 5 persona",
            BusinessConstraints = "Version 5 constraints",
            IsActive = false,
            Version = 5
        };

        await repo.CreateAsync(instruction);
        var retrieved = await repo.GetByVersionAsync(5);

        Assert.NotNull(retrieved);
        Assert.Equal(5, retrieved.Version);
    }
}

