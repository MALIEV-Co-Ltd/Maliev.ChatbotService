using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for UserMemory repository CRUD operations and preference queries.
/// </summary>
[Collection("Database")]
public class UserMemoryRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public UserMemoryRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private async Task<(UserProfile userProfile, ConversationSession session, Message message)> CreateTestDataAsync(IServiceScope scope)
    {
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var session = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            Language = Language.English,
            Status = SessionStatus.Active,
            StartTime = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        await sessionRepo.CreateAsync(session);

        var message = new Message
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = "Test message",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepo.CreateAsync(message);

        return (userProfile, session, message);
    }
    /// <summary>
    /// Tests that a valid user memory can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidUserMemory_ReturnsCreatedMemory()
    {
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var (userProfile, _, message) = await CreateTestDataAsync(scope);

        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "MaterialPreference",
            Value = "{\"material\":\"stainless steel 304\"}",
            Confidence = 0.95,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        var created = await memoryRepo.CreateAsync(memory);

        Assert.NotNull(created);
        Assert.Equal("MaterialPreference", created.Key);
        Assert.Equal(0.95, created.Confidence);
    }

    /// <summary>
    /// Tests that multiple user memories can be retrieved by user ID.
    /// </summary>
    [Fact]
    public async Task GetMemoriesByUserIdAsync_MultipleMemories_ReturnsAllMemories()
    {
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var (userProfile, _, message) = await CreateTestDataAsync(scope);

        var memory1 = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "DeliveryAddress",
            Value = "{\"address\":\"123 Industrial Park\"}",
            Confidence = 0.9,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        var memory2 = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "LanguagePreference",
            Value = "{\"language\":\"Thai\"}",
            Confidence = 1.0,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await memoryRepo.CreateAsync(memory1);
        await memoryRepo.CreateAsync(memory2);

        var memories = await memoryRepo.GetMemoriesByUserIdAsync(userProfile.Id);

        Assert.Equal(2, memories.Count());
    }

    /// <summary>
    /// Tests that user memory confidence can be updated successfully.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_UpdateConfidence_UpdatesSuccessfully()
    {
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var (userProfile, _, message) = await CreateTestDataAsync(scope);

        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "ColorPreference",
            Value = "{\"color\":\"blue\"}",
            Confidence = 0.7,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await memoryRepo.CreateAsync(memory);

        memory.Confidence = 0.95;
        memory.LastUpdatedAt = DateTimeOffset.UtcNow;
        await memoryRepo.UpdateAsync(memory);

        var updated = await memoryRepo.GetByIdAsync(memory.Id);
        Assert.NotNull(updated);
        Assert.Equal(0.95, updated.Confidence);
    }

    /// <summary>
    /// Tests that a user memory can be deleted successfully.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ExistingMemory_DeletesSuccessfully()
    {
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var (userProfile, _, message) = await CreateTestDataAsync(scope);

        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "TempPreference",
            Value = "{\"temp\":\"value\"}",
            Confidence = 0.5,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await memoryRepo.CreateAsync(memory);
        await memoryRepo.DeleteAsync(memory.Id);
        var retrieved = await memoryRepo.GetByIdAsync(memory.Id);

        Assert.Null(retrieved);
    }

    /// <summary>
    /// Tests that a user memory can be retrieved by key.
    /// </summary>
    [Fact]
    public async Task GetMemoryByKeyAsync_ExistingKey_ReturnsMemory()
    {
        using var scope = _factory.Services.CreateScope();
        var memoryRepo = scope.ServiceProvider.GetRequiredService<IUserMemoryRepository>();
        var (userProfile, _, message) = await CreateTestDataAsync(scope);

        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Key = "PreferredVendor",
            Value = "{\"vendor\":\"ACME Corp\"}",
            Confidence = 0.85,
            SourceMessageId = message.Id,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        await memoryRepo.CreateAsync(memory);
        var retrieved = await memoryRepo.GetMemoryByKeyAsync(userProfile.Id, "PreferredVendor");

        Assert.NotNull(retrieved);
        Assert.Equal("PreferredVendor", retrieved.Key);
    }
}

