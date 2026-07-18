using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

[Collection("Database")]
public class KnowledgeBaseApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public KnowledgeBaseApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateAuthenticatedClient(string[] permissions) => _factory.CreateAuthenticatedClient(permissions);

    [Fact]
    public async Task GetEntries_WithAuthentication_ReturnsEntries()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.read" });

        var response = await client.GetAsync("/chatbot/v1/admin/knowledge-base");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEntries_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/chatbot/v1/admin/knowledge-base");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEntries_WithoutPermission_Returns403()
    {
        // Deny the knowledge.read permission in the mock
        MockIAMServiceClient.DenyPermission("chatbot.knowledge.read");

        var client = CreateAuthenticatedClient(new[] { "chatbot.preferences.read" });

        var response = await client.GetAsync("/chatbot/v1/admin/knowledge-base");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEntries_WithPagination_ReturnsPaginatedResults()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.read" });

        var response = await client.GetAsync("/chatbot/v1/admin/knowledge-base?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEntries_WithTopicFilter_ReturnsFilteredResults()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.read" });

        var response = await client.GetAsync("/chatbot/v1/admin/knowledge-base?topicKey=FDM");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEntry_WithValidId_ReturnsEntry()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });

        var createRequest = new CreateKnowledgeBaseRequest
        {
            TopicKey = "FDM",
            FactKey = "Material",
            Content = "PLA is a common FDM material"
        };
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<KnowledgeBaseDto>(_factory.JsonSerializerOptions);

        var getClient = CreateAuthenticatedClient(new[] { "chatbot.knowledge.read" });
        var getResponse = await getClient.GetAsync($"/chatbot/v1/admin/knowledge-base/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetEntry_WithInvalidId_Returns404()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.read" });

        var response = await client.GetAsync($"/chatbot/v1/admin/knowledge-base/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEntry_WithValidData_ReturnsCreated()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });
        var request = new CreateKnowledgeBaseRequest
        {
            TopicKey = "FDM",
            FactKey = "LayerHeight",
            Content = "Standard layer heights: 0.12mm, 0.16mm, 0.20mm"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<KnowledgeBaseDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("FDM", result.TopicKey);
    }

    [Fact]
    public async Task CreateEntry_WithoutPermission_Returns403()
    {
        // Deny the knowledge.write permission in the mock
        MockIAMServiceClient.DenyPermission("chatbot.knowledge.write");

        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.read" });
        var request = new CreateKnowledgeBaseRequest
        {
            TopicKey = "FDM",
            FactKey = "Test",
            Content = "Test content"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEntry_WithValidData_ReturnsUpdated()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });

        var createRequest = new CreateKnowledgeBaseRequest
        {
            TopicKey = "FDM",
            FactKey = "Test",
            Content = "Original content"
        };
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<KnowledgeBaseDto>(_factory.JsonSerializerOptions);

        var updateRequest = new UpdateKnowledgeBaseRequest
        {
            Content = "Updated content"
        };

        var updateResponse = await client.PutAsJsonAsync($"/chatbot/v1/admin/knowledge-base/{created!.Id}", updateRequest, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<KnowledgeBaseDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated content", updated!.Content);
    }

    [Fact]
    public async Task UpdateEntry_WithInvalidId_Returns404()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });
        var request = new UpdateKnowledgeBaseRequest
        {
            Content = "Updated content"
        };

        var response = await client.PutAsJsonAsync($"/chatbot/v1/admin/knowledge-base/{Guid.NewGuid()}", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEntry_WithValidId_ReturnsNoContent()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });

        var createRequest = new CreateKnowledgeBaseRequest
        {
            TopicKey = "FDM",
            FactKey = "ToDelete",
            Content = "Content to delete"
        };
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/knowledge-base", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<KnowledgeBaseDto>(_factory.JsonSerializerOptions);

        var deleteResponse = await client.DeleteAsync($"/chatbot/v1/admin/knowledge-base/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEntry_WithInvalidId_Returns404()
    {
        var client = CreateAuthenticatedClient(new[] { "chatbot.knowledge.write" });

        var response = await client.DeleteAsync($"/chatbot/v1/admin/knowledge-base/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
