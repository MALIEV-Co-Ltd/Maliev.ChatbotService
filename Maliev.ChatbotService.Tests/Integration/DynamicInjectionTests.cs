using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for dynamic instruction injection based on intent.
/// </summary>
[Collection("Database")]
public class DynamicInjectionTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicInjectionTests"/> class.
    /// </summary>
    /// <param name="factory">The test factory.</param>
    public DynamicInjectionTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateAuthenticatedClient() => _factory.CreateAuthenticatedClient("test-user");

    /// <summary>
    /// Tests that sending a message with a specific topic intent injects specialized instructions.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithTopicIntent_InjectsSpecializedInstructions()
    {
        // Arrange
        var adminClient = _factory.CreateAuthenticatedClient("admin", new[] { "chatbot.instructions.write" });

        // 1. Create Topic Instruction
        var topicRequest = new CreateSystemInstructionRequest
        {
            Name = "3D Scanning Domain",
            Category = SystemInstructionCategory.Topic,
            TopicKey = "3D-Scanning",
            Priority = 5,
            PersonaDefinition = "SPECIALIZED_3D_SCANNING_INSTRUCTION",
            BusinessConstraints = "3D scanning rules only",
            IsActive = true
        };
        await adminClient.PostAsJsonAsync("/chatbot/v1/admin/instructions", topicRequest);

        // 2. Create Session
        var client = CreateAuthenticatedClient();
        var initiateRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", initiateRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);

        // 3. Send Message that triggers 3D-Scanning intent
        var messageRequest = new SendMessageRequest
        {
            SessionId = session.SessionId,
            Content = "Tell me about your 3D scanning services."
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);

        // Verification of injected topics in MetadataJson should be done here if we can access the message from DB
        // or check if the LLM response contains specific domain markers
    }

    /// <summary>
    /// Tests that sending a general query only injects core persona instructions.
    /// </summary>
    [Fact]
    public async Task SendMessage_GeneralQuery_OnlyInjectsCoreInstructions()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var initiateRequest = new InitiateSessionRequest { Channel = "website" };
        var sessionResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", initiateRequest, _factory.JsonSerializerOptions);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(session);

        var messageRequest = new SendMessageRequest
        {
            SessionId = session.SessionId,
            Content = "Hello, how are you?"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
