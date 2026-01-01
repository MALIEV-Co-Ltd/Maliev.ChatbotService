using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Contract;

/// <summary>
/// Contract tests for session initiation (User Story 1).
/// </summary>
[Collection("Database")]
public class SessionInitiationTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public SessionInitiationTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    /// <summary>
    /// Tests that POST /v1/sessions/initiate returns session with English default language.
    /// </summary>
    [Fact]
    public async Task InitiateSession_WithValidRequest_ReturnsSessionWithEnglishDefault()
    {
        // Arrange
        var client = CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "website"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        Assert.False(string.IsNullOrEmpty(session.WelcomeMessage));
        Assert.Equal("en", session.Language);
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Tests that POST /v1/sessions/initiate with Thai message returns session with Thai language.
    /// </summary>
    [Fact]
    public async Task InitiateSession_WithThaiLanguage_ReturnsSessionWithThaiLanguage()
    {
        // Arrange
        var client = CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "website",
            Language = "th"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);
        Assert.Equal("th", session.Language);
        Assert.Contains("สวัสดี", session.WelcomeMessage); // Thai greeting
    }

    /// <summary>
    /// Tests that session initiation on LINE channel works correctly.
    /// </summary>
    [Fact]
    public async Task InitiateSession_OnLineChannel_ReturnsValidSession()
    {
        // Arrange
        var client = CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "line",
            ExternalUserId = "U1234567890"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);
        Assert.NotEqual(Guid.Empty, session.SessionId);
    }

    /// <summary>
    /// Tests that invalid channel returns bad request.
    /// </summary>
    [Fact]
    public async Task InitiateSession_InvalidChannel_ReturnsBadRequest()
    {
        // Arrange
        var client = CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "invalid_channel"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that session expiry is set to 24 hours.
    /// </summary>
    [Fact]
    public async Task InitiateSession_ValidRequest_SetsExpiry24Hours()
    {
        // Arrange
        var client = CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "website"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        // Assert
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);

        var expectedExpiry = DateTimeOffset.UtcNow.AddHours(24);
        var timeDifference = (session.ExpiresAt - expectedExpiry).Duration();
        Assert.True(timeDifference < TimeSpan.FromMinutes(1));
    }
}

