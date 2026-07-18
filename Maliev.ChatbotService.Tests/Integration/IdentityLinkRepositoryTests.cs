using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for IdentityLink repository CRUD operations and platform linking queries.
/// </summary>
[Collection("Database")]
public class IdentityLinkRepositoryTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public IdentityLinkRepositoryTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that a valid identity link can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ValidIdentityLink_ReturnsCreatedLink()
    {
        using var scope = _factory.Services.CreateScope();
        var linkRepo = scope.ServiceProvider.GetRequiredService<IIdentityLinkRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var link = new IdentityLink
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            PlatformName = PlatformName.Line,
            ExternalPlatformId = "U1234567890",
            WebhookConfirmationStatus = WebhookConfirmationStatus.Pending,
            LinkCreatedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow
        };

        var created = await linkRepo.CreateAsync(link);

        Assert.NotNull(created);
        Assert.Equal(PlatformName.Line, created.PlatformName);
        Assert.Equal(WebhookConfirmationStatus.Pending, created.WebhookConfirmationStatus);
    }

    /// <summary>
    /// Tests that an identity link can be retrieved by platform ID.
    /// </summary>
    [Fact]
    public async Task GetByPlatformIdAsync_ExistingLink_ReturnsLink()
    {
        using var scope = _factory.Services.CreateScope();
        var linkRepo = scope.ServiceProvider.GetRequiredService<IIdentityLinkRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var link = new IdentityLink
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            PlatformName = PlatformName.Facebook,
            ExternalPlatformId = "FB987654321",
            WebhookConfirmationStatus = WebhookConfirmationStatus.Confirmed,
            LinkCreatedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow
        };

        await linkRepo.CreateAsync(link);
        var retrieved = await linkRepo.GetByPlatformIdAsync(PlatformName.Facebook, "FB987654321");

        Assert.NotNull(retrieved);
        Assert.Equal(userProfile.Id, retrieved.UserProfileId);
    }

    /// <summary>
    /// Tests that webhook confirmation status can be updated successfully.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ConfirmWebhook_UpdatesStatusSuccessfully()
    {
        using var scope = _factory.Services.CreateScope();
        var linkRepo = scope.ServiceProvider.GetRequiredService<IIdentityLinkRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var link = new IdentityLink
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            PlatformName = PlatformName.WhatsApp,
            ExternalPlatformId = "WA555444333",
            WebhookConfirmationStatus = WebhookConfirmationStatus.Pending,
            LinkCreatedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow
        };

        await linkRepo.CreateAsync(link);
        link.WebhookConfirmationStatus = WebhookConfirmationStatus.Confirmed;
        link.LastVerifiedAt = DateTimeOffset.UtcNow;
        await linkRepo.UpdateAsync(link);

        var updated = await linkRepo.GetByIdAsync(link.Id);
        Assert.NotNull(updated);
        Assert.Equal(WebhookConfirmationStatus.Confirmed, updated.WebhookConfirmationStatus);
    }

    /// <summary>
    /// Tests that multiple identity links for a user are returned.
    /// </summary>
    [Fact]
    public async Task GetLinksByUserIdAsync_MultipleLinks_ReturnsAllLinks()
    {
        using var scope = _factory.Services.CreateScope();
        var linkRepo = scope.ServiceProvider.GetRequiredService<IIdentityLinkRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var link1 = new IdentityLink
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            PlatformName = PlatformName.Line,
            ExternalPlatformId = "U111111",
            WebhookConfirmationStatus = WebhookConfirmationStatus.Confirmed,
            LinkCreatedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow
        };

        var link2 = new IdentityLink
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            PlatformName = PlatformName.Instagram,
            ExternalPlatformId = "IG222222",
            WebhookConfirmationStatus = WebhookConfirmationStatus.Confirmed,
            LinkCreatedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow
        };

        await linkRepo.CreateAsync(link1);
        await linkRepo.CreateAsync(link2);

        var links = await linkRepo.GetLinksByUserIdAsync(userProfile.Id);

        Assert.Equal(2, links.Count());
    }
}

