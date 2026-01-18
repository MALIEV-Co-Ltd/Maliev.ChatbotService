using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for LinkIdentityCommandHandler.
/// </summary>
public class LinkIdentityCommandHandlerTests : IClassFixture<BaseIntegrationTestFactory<Program, ChatbotDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkIdentityCommandHandlerTests"/> class.
    /// </summary>
    public LinkIdentityCommandHandlerTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Tests that a valid link identity command successfully links the identity and updates the profile.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ValidCommand_ShouldLinkIdentityAndUpdatedProfile()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var linkRepo = scope.ServiceProvider.GetRequiredService<IIdentityLinkRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LinkIdentityCommandHandler>>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var handler = new LinkIdentityCommandHandler(linkRepo, userRepo, logger);
        var command = new LinkIdentityCommand
        {
            UserProfileId = userProfile.Id,
            PlatformName = "line",
            ExternalUserId = "U_TEST_123"
        };

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PlatformName.Line, result.PlatformName);
        Assert.Equal("U_TEST_123", result.ExternalUserId);

        var updatedProfile = await userRepo.GetByIdAsync(userProfile.Id);
        Assert.Equal("U_TEST_123", updatedProfile!.LineUserId);
        
        var links = await linkRepo.GetByUserIdAsync(userProfile.Id);
        Assert.Contains(links, l => l.ExternalPlatformId == "U_TEST_123" && l.PlatformName == PlatformName.Line);
    }
}
