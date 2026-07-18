using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task InitiateSession_WithAnonymousIntranetChannel_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new InitiateSessionRequest
        {
            Channel = "intranet",
            Language = "en"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InitiateSession_WithAuthenticatedIntranetUser_StoresSessionsUnderEmployeeProfile()
    {
        var employeeId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient([ChatbotPermissions.SessionCreate], employeeId.ToString());
        var request = new InitiateSessionRequest
        {
            Channel = "intranet",
            Language = "en"
        };

        var firstResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);
        var secondResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();
        var sessions = await sessionRepo.GetSessionsByUserIdAsync(employeeId);

        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, session =>
        {
            Assert.Equal(employeeId, session.UserProfileId);
            Assert.Equal(Channel.Intranet, session.Channel);
        });
    }

    [Fact]
    public async Task GetMySessions_WithAuthenticatedEmployee_ReturnsOnlyTheirIntranetConversations()
    {
        var employeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
            var sessionRepo = scope.ServiceProvider.GetRequiredService<IConversationSessionRepository>();

            await userRepo.CreateAsync(new UserProfile
            {
                Id = employeeId,
                Role = UserRole.InternalAgent,
                CreatedAt = now,
                LastActiveAt = now
            });

            await userRepo.CreateAsync(new UserProfile
            {
                Id = otherEmployeeId,
                Role = UserRole.InternalAgent,
                CreatedAt = now,
                LastActiveAt = now
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = employeeId,
                Channel = Channel.Intranet,
                StartTime = now.AddMinutes(-20),
                LastActivityAt = now.AddMinutes(-10),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = employeeId,
                Channel = Channel.Line,
                StartTime = now.AddMinutes(-15),
                LastActivityAt = now.AddMinutes(-12),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });

            await sessionRepo.CreateAsync(new ConversationSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = otherEmployeeId,
                Channel = Channel.Intranet,
                StartTime = now.AddMinutes(-5),
                LastActivityAt = now.AddMinutes(-4),
                ExpiresAt = now.AddHours(23),
                Language = Language.English,
                Status = SessionStatus.Active
            });
        }

        var client = _factory.CreateAuthenticatedClient([ChatbotPermissions.SessionRead], employeeId.ToString());

        var response = await client.GetAsync("/chatbot/v1/sessions?channel=intranet&page=1&page_size=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversationSessionsResponse>(_factory.JsonSerializerOptions);

        Assert.NotNull(result);
        Assert.Single(result.Data);
        Assert.Equal(employeeId, result.Data[0].UserProfileId);
        Assert.Equal("intranet", result.Data[0].Channel);
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
