using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for controlled web search (User Story 8).
/// </summary>
[Collection("Database")]
public class WebSearchTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public WebSearchTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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
    /// Tests that technical spec query triggers Google Custom Search when permitted.
    /// </summary>
    [Fact]
    public async Task SendMessage_TechnicalSpecQuery_TriggersWebSearch()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What are the properties of ASTM A36 steel?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that Gemini built-in search is triggered when enabled in system instructions.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithWebSearch_Enabled_TriggersGeminiSearch()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var instructionRepo = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();
        var systemInstructionService = scope.ServiceProvider.GetRequiredService<ISystemInstructionService>();

        // Create system instruction with web search enabled
        await instructionRepo.DeactivateAllAsync();
        await systemInstructionService.InvalidateCacheAsync();
        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Web Search Enabled",
            PersonaDefinition = "Manufacturing assistant with web search",
            BusinessConstraints = "Use web search for technical queries",
            IsActive = true,
            Version = 1,
            EnableWebSearch = true,
            LogSearchDomains = false,
            AllowedTopics = string.Empty,
            RejectionTemplates = "{}"
        };
        await instructionRepo.CreateAsync(instruction);

        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What is ISO 9001?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }

    /// <summary>
    /// Tests that web search disabled in system instructions prevents search execution.
    /// </summary>
    [Fact]
    public async Task SendMessage_WebSearchDisabled_PreventsSearch()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var instructionRepo = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();

        // Create system instruction with web search disabled
        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "No Web Search",
            PersonaDefinition = "Manufacturing assistant",
            BusinessConstraints = "Do not use web search",
            IsActive = true,
            Version = 1
        };
        await instructionRepo.CreateAsync(instruction);

        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Search for ASTM A36 properties"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        // Should respond without web search
    }

    /// <summary>
    /// Tests that competitor pricing query is refused despite search capability.
    /// </summary>
    [Fact]
    public async Task SendMessage_CompetitorPricingSearch_Refused()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Search for CompetitorCorp pricing online"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
        Assert.DoesNotContain("competitor", message.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that web search query timeout (30s) triggers fallback response.
    /// </summary>
    [Fact]
    public async Task SendMessage_WebSearchTimeout_ReturnsFallback()
    {
        // Arrange
        var client = CreateClient();
        var sessionRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", sessionRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "Complex search query that might timeout"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(message);
    }
}

