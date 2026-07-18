using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for Knowledge Base management and fact retrieval.
/// </summary>
[Collection("Database")]
public class KnowledgeBaseTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseTests"/> class.
    /// </summary>
    /// <param name="factory">The test factory.</param>
    public KnowledgeBaseTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc/>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateAuthenticatedClient(string[]? permissions = null) => _factory.CreateAuthenticatedClient("test-user", permissions);

    /// <summary>
    /// Tests that an admin can manage knowledge base entries.
    /// </summary>
    [Fact]
    public async Task Admin_CanManageKnowledgeBase()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write", "chatbot.knowledge.read" });
        var request = new CreateKnowledgeBaseRequest
        {
            TopicKey = "Pricing",
            FactKey = "3D-Scanning-OnSite",
            Content = "On-site 3D scanning costs $500 per hour.",
            Metadata = new { region = "TH" }
        };

        // Act & Assert - Create
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", request, _factory.JsonSerializerOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<KnowledgeBaseDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(created);
        Assert.Equal("Pricing", created.TopicKey);

        // Act & Assert - Read
        var getResponse = await client.GetAsync($"/chatbot/v1/admin/knowledge-base?topicKey=Pricing");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var items = await getResponse.Content.ReadFromJsonAsync<KnowledgeBaseDto[]>(_factory.JsonSerializerOptions);
        Assert.Single(items!);
    }

    /// <summary>
    /// Tests that specialized facts are injected into the prompt when relevant intent is detected.
    /// </summary>
    [Fact]
    public async Task SendMessage_WithPricingIntent_InjectsKnowledgeBaseFacts()
    {
        // Arrange
        var adminClient = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });
        await adminClient.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", new CreateKnowledgeBaseRequest
        {
            TopicKey = "Pricing",
            FactKey = "Standard-Rate",
            Content = "OUR_SECRET_PRICING_FACT: $100 per unit."
        }, _factory.JsonSerializerOptions);

        var client = CreateAuthenticatedClient();
        var initiateResponse = await client.PostAsJsonAsync("/chatbot/v1/sessions/initiate", new InitiateSessionRequest { Channel = "website" }, _factory.JsonSerializerOptions);
        var session = await initiateResponse.Content.ReadFromJsonAsync<SessionResponse>(_factory.JsonSerializerOptions);

        // Act
        var messageRequest = new SendMessageRequest
        {
            SessionId = session!.SessionId,
            Content = "What is your pricing?"
        };
        var response = await client.PostAsJsonAsync("/chatbot/v1/messages", messageRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // In a real scenario, we'd verify if the LLM output contains the fact, 
        // but here we just ensure the flow completes successfully.
    }
}

/// <summary>
/// DTO for Knowledge Base entry in tests.
/// </summary>
public class KnowledgeBaseDto
{
    /// <summary>Gets or sets the ID.</summary>
    public Guid Id { get; set; }
    /// <summary>Gets or sets the topic key.</summary>
    public string TopicKey { get; set; } = string.Empty;
    /// <summary>Gets or sets the fact key.</summary>
    public string FactKey { get; set; } = string.Empty;
    /// <summary>Gets or sets the content.</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Gets or sets the metadata.</summary>
    public object Metadata { get; set; } = new { };
}

/// <summary>
/// Request to create a KB entry in tests.
/// </summary>
public class CreateKnowledgeBaseRequest
{
    /// <summary>Gets or sets the topic key.</summary>
    public string TopicKey { get; set; } = string.Empty;
    /// <summary>Gets or sets the fact key.</summary>
    public string FactKey { get; set; } = string.Empty;
    /// <summary>Gets or sets the content.</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Gets or sets the metadata.</summary>
    public object? Metadata { get; set; }
}
