using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Xunit;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for System Instruction API.
/// </summary>
public class SystemInstructionApiTests : IClassFixture<BaseIntegrationTestFactory<Program, ChatbotDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;
    private readonly HttpClient _adminClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionApiTests"/> class.
    /// </summary>
    public SystemInstructionApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
        // Use authenticated client with admin permissions (wildcard)
        _adminClient = factory.CreateAuthenticatedClient();
    }

    /// <summary>
    /// Tests creating and retrieving a system instruction.
    /// </summary>
    [Fact]
    public async Task CreateAndGetInstruction_ShouldReturnSuccess()
    {
        // Arrange
        var request = new CreateSystemInstructionRequest
        {
            Name = "Test Instruction",
            Category = SystemInstructionCategory.Core,
            TopicKey = "test-topic",
            Priority = 10,
            PersonaDefinition = "Test Persona Definition Long Enough",
            BusinessConstraints = "Test Constraints Definition Long Enough",
            IsActive = true
        };

        // Act - Create
        var createResponse = await _adminClient.PostAsJsonAsync("/chatbot/v1/admin/instructions", request, _factory.JsonSerializerOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        
        var created = await createResponse.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);

        // Act - Get
        var getResponse = await _adminClient.GetAsync($"/chatbot/v1/admin/instructions?category={request.Category}&topicKey={request.TopicKey}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var instructions = await getResponse.Content.ReadFromJsonAsync<IEnumerable<SystemInstructionDto>>(_factory.JsonSerializerOptions);
        Assert.Contains(instructions!, i => i.Id == created.Id);
    }

    /// <summary>
    /// Tests updating a system instruction.
    /// </summary>
    [Fact]
    public async Task UpdateInstruction_ShouldReturnSuccess()
    {
        // Arrange - Create first
        var createRequest = new CreateSystemInstructionRequest
        {
            Name = "Update Me",
            Category = SystemInstructionCategory.Topic,
            TopicKey = "update-test",
            PersonaDefinition = "Test Persona Definition Long Enough",
            BusinessConstraints = "Test Constraints Definition Long Enough",
            IsActive = true
        };
        var createResponse = await _adminClient.PostAsJsonAsync("/chatbot/v1/admin/instructions", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);

        var updateRequest = new UpdateSystemInstructionRequest
        {
            Name = "Updated Name",
            IsActive = false
        };

        // Act - Update
        var updateResponse = await _adminClient.PutAsJsonAsync($"/chatbot/v1/admin/instructions/{created.Id}", updateRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.False(updated.IsActive);
    }
}
