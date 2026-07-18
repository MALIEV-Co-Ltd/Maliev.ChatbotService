using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for UserProfile repository CRUD operations.
/// </summary>
[Collection("Database")]
public class UserProfileRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public UserProfileRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that a user profile can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidUserProfile_ReturnsCreatedProfile()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            LineUserId = "U1234567890",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        // Act
        var created = await repository.CreateAsync(userProfile);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(userProfile.LineUserId, created.LineUserId);
        Assert.Equal(UserRole.Customer, created.Role);
    }

    /// <summary>
    /// Tests that a user profile can be retrieved by ID.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_ExistingProfile_ReturnsProfile()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            FacebookId = "FB123456",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(userProfile);

        // Act
        var retrieved = await repository.GetByIdAsync(userProfile.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(userProfile.Id, retrieved.Id);
        Assert.Equal("FB123456", retrieved.FacebookId);
    }

    /// <summary>
    /// Tests that GetByIdAsync returns null for non-existent profile.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_NonExistentProfile_ReturnsNull()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var nonExistentId = Guid.NewGuid();

        // Act
        var retrieved = await repository.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(retrieved);
    }

    /// <summary>
    /// Tests that a user profile can be found by LINE user ID.
    /// </summary>
    [Fact]
    public async Task GetByLineUserIdAsync_ExistingLineId_ReturnsProfile()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            LineUserId = "U9876543210",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(userProfile);

        // Act
        var retrieved = await repository.GetByLineUserIdAsync("U9876543210");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(userProfile.Id, retrieved.Id);
        Assert.Equal("U9876543210", retrieved.LineUserId);
    }

    /// <summary>
    /// Tests that a user profile can be found by Facebook ID.
    /// </summary>
    [Fact]
    public async Task GetByFacebookIdAsync_ExistingFacebookId_ReturnsProfile()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            FacebookId = "FB999888777",
            Role = UserRole.InternalAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(userProfile);

        // Act
        var retrieved = await repository.GetByFacebookIdAsync("FB999888777");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(userProfile.Id, retrieved.Id);
        Assert.Equal(UserRole.InternalAgent, retrieved.Role);
    }

    /// <summary>
    /// Tests that a user profile can be updated successfully.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ExistingProfile_UpdatesSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            WhatsAppId = "WA12345",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(userProfile);

        // Act
        userProfile.LastActiveAt = DateTimeOffset.UtcNow.AddHours(1);
        userProfile.InternalUserId = "CUST-001";
        await repository.UpdateAsync(userProfile);

        // Assert
        var updated = await repository.GetByIdAsync(userProfile.Id);
        Assert.NotNull(updated);
        Assert.Equal("CUST-001", updated.InternalUserId);
        Assert.True(updated.LastActiveAt > userProfile.CreatedAt);
    }

    /// <summary>
    /// Tests that a user profile can be deleted successfully.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ExistingProfile_DeletesSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            InstagramId = "IG54321",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(userProfile);

        // Act
        await repository.DeleteAsync(userProfile.Id);
        var retrieved = await repository.GetByIdAsync(userProfile.Id);

        // Assert
        Assert.Null(retrieved);
    }

    /// <summary>
    /// Tests that internal user ID can be used to find profile.
    /// </summary>
    [Fact]
    public async Task GetByInternalUserIdAsync_ExistingInternalId_ReturnsProfile()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            InternalUserId = "EMP-123",
            Role = UserRole.InternalAgent,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(userProfile);

        // Act
        var retrieved = await repository.GetByInternalUserIdAsync("EMP-123");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("EMP-123", retrieved.InternalUserId);
        Assert.Equal(UserRole.InternalAgent, retrieved.Role);
    }
}

