using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for cross-platform continuity (User Story 2).
/// </summary>
[Collection("Database")]
public class CrossPlatformContinuityTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public CrossPlatformContinuityTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private HttpClient CreateClient() => _factory.CreateClient();
    private HttpClient CreateAuthenticatedClient(string[]? permissions = null) => _factory.CreateAuthenticatedClient("test-user", permissions);
    /// <summary>
    /// Tests that session initiated on website, continued on LINE retrieves context.
    /// </summary>
    [Fact]
    public async Task SessionContinuity_WebsiteToLine_RetrievesContext()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();

        // Create user profile with both website and LINE identities
        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            LineUserId = "U9999888877",
            InternalUserId = "CUST-100",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        // Create a closed session on website
        var websiteSession = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow.AddHours(-2),
            LastActivityAt = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(23),
            Language = Language.English,
            Status = SessionStatus.Closed
        };
        await sessionRepo.CreateAsync(websiteSession);

        var client = CreateClient();
        var lineSessionRequest = new InitiateSessionRequest
        {
            Channel = "line",
            ExternalUserId = "U9999888877"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", lineSessionRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);
    }

    /// <summary>
    /// Tests that previous session summary is retrieved and included in new session context.
    /// </summary>
    [Fact]
    public async Task NewSession_WithPreviousSummary_IncludesSummaryInContext()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var summaryRepo = scope.ServiceProvider.GetRequiredService<IConversationSummaryRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var oldSession = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow.AddDays(-1),
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            Language = Language.English,
            Status = SessionStatus.Closed
        };
        await sessionRepo.CreateAsync(oldSession);

        var summary = new ConversationSummary
        {
            Id = Guid.NewGuid(),
            SessionId = oldSession.Id,
            UserProfileId = userProfile.Id,
            StructuredSummary = "{\"topics\":[\"CNC machining\"],\"decisions\":[\"Interested in aluminum parts\"],\"preferences\":[\"Prefers 6061 aluminum\"],\"entities\":[],\"intentCategories\":[\"quotation_request\"],\"unresolvedQuestions\":[]}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await summaryRepo.CreateAsync(summary);

        oldSession.SummaryId = summary.Id;
        await sessionRepo.UpdateAsync(oldSession);

        var client = _factory.CreateAuthenticatedClient(userProfile.Id.ToString());
        var newSessionRequest = new InitiateSessionRequest { Channel = "website" };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", newSessionRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newSession = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(newSession);
        Assert.Contains("aluminum", newSession.WelcomeMessage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that session expires after 24 hours and conversation summary is generated.
    /// </summary>
    [Fact]
    public async Task SessionExpiry_After24Hours_GeneratesSummary()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var summaryService = scope.ServiceProvider.GetRequiredService<IConversationSummaryService>();

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
            StartTime = DateTimeOffset.UtcNow.AddDays(-2),
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            Language = Language.English,
            Status = SessionStatus.Active
        };
        await sessionRepo.CreateAsync(session);

        // Act
        var summary = await summaryService.GenerateSummaryAsync(session.Id);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(session.Id, summary.SessionId);
        Assert.False(string.IsNullOrEmpty(summary.StructuredSummary));
    }

    /// <summary>
    /// Tests that language consistency is maintained across platform switch.
    /// </summary>
    [Fact]
    public async Task PlatformSwitch_ThaiLanguage_MaintainsLanguageConsistency()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            LineUserId = "U7777666655",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);

        var thaiSession = new ConversationSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfile.Id,
            Channel = Channel.Website,
            StartTime = DateTimeOffset.UtcNow.AddHours(-1),
            LastActivityAt = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(23),
            Language = Language.Thai,
            Status = SessionStatus.Closed
        };
        await sessionRepo.CreateAsync(thaiSession);

        var client = CreateClient();
        var lineSessionRequest = new InitiateSessionRequest
        {
            Channel = "line",
            ExternalUserId = "U7777666655"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", lineSessionRequest, _factory.JsonSerializerOptions);

        // Assert
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);
        Assert.Equal("th", session.Language);
    }
}

