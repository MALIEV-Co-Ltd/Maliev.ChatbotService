using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Tests.Integration;

[Collection("Database")]
public class SessionsApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public SessionsApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitiateSession_WithValidData_ReturnsSessionId()
    {
        var client = _factory.CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "line",
            ExternalUserId = "U123456",
            Language = "en"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SessionId);
    }

    [Fact]
    public async Task InitiateSession_WithThaiLanguage_ReturnsThai()
    {
        var client = _factory.CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "line",
            ExternalUserId = "U123456",
            Language = "th"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("th", result.Language);
    }

    [Fact]
    public async Task InitiateSession_WithInvalidChannel_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "invalid-channel",
            ExternalUserId = "U123456"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LinkIdentity_WithValidData_ReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(new[] { "chatbot.users.link" }, userId.ToString());
        
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var userProfile = new UserProfile
        {
            Id = userId,
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActiveAt = DateTimeOffset.UtcNow
        };
        await userRepo.CreateAsync(userProfile);
        
        var request = new LinkIdentityRequest
        {
            PlatformName = "line",
            ExternalUserId = "U123456"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/link", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkIdentity_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new LinkIdentityRequest
        {
            PlatformName = "line",
            ExternalUserId = "U123456"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/link", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
