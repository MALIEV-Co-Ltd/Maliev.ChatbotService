using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for Admin API endpoints for system instructions.
/// </summary>
[Collection("Database")]
public class AdminApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public AdminApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private HttpClient CreateAuthenticatedClient(string[]? permissions = null) => _factory.CreateAuthenticatedClient("test-user", permissions);
    /// <summary>
    /// Tests that GET /v1/admin/instructions with authentication returns system instructions list.
    /// </summary>
    [Fact]
    public async Task GetInstructions_WithAuthentication_ReturnsInstructionsList()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.read" });

        // Act
        var response = await client.GetAsync("/chatbot/v1/admin/instructions?activeOnly=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var instructions = await response.Content.ReadFromJsonAsync<SystemInstructionDto[]>(_factory.JsonSerializerOptions);
        Assert.NotNull(instructions);
    }

    /// <summary>
    /// Tests that POST /v1/admin/instructions with authentication creates new instruction.
    /// </summary>
    [Fact]
    public async Task CreateInstruction_WithAuthentication_CreatesInstruction()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.write" });
        var request = new CreateSystemInstructionRequest
        {
            Name = "Test Instruction",
            PersonaDefinition = "You are a test assistant",
            BusinessConstraints = "Test constraints only",
            IsActive = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var instruction = await response.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(instruction);
        Assert.Equal("Test Instruction", instruction.Name);
    }

    /// <summary>
    /// Tests that POST /v1/admin/instructions with authentication creates new categorized instruction.
    /// </summary>
    [Fact]
    public async Task CreateCategorizedInstruction_WithAuthentication_CreatesInstruction()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.write" });
        var request = new CreateSystemInstructionRequest
        {
            Name = "Topic Instruction",
            Category = SystemInstructionCategory.Topic,
            TopicKey = "3D-Scanning",
            Priority = 10,
            PersonaDefinition = "You are a 3D scanning expert",
            BusinessConstraints = "3D scanning rules only",
            IsActive = true
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var instruction = await response.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(instruction);
        Assert.Equal(SystemInstructionCategory.Topic, instruction.Category);
        Assert.Equal("3D-Scanning", instruction.TopicKey);
        Assert.Equal(10, instruction.Priority);
    }

    /// <summary>
    /// Tests that PUT /v1/admin/instructions/{id} with authentication updates instruction.
    /// </summary>
    [Fact]
    public async Task UpdateInstruction_WithAuthentication_UpdatesInstruction()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.write" });

        // First create an instruction
        var createRequest = new CreateSystemInstructionRequest
        {
            Name = "Original Instruction",
            PersonaDefinition = "Original persona",
            BusinessConstraints = "Original constraints",
            IsActive = false
        };
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);

        var updateRequest = new UpdateSystemInstructionRequest
        {
            Name = "Updated Instruction",
            PersonaDefinition = "Updated persona",
            IsActive = true
        };

        // Act
        Assert.NotNull(created);
        var response = await client.PutAsJsonAsync($"/chatbot/v1/admin/instructions/{created.Id}", updateRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated Instruction", updated.Name);
        Assert.True(updated.IsActive);
    }

    /// <summary>
    /// Tests that PUT /v1/admin/instructions/{id} with authentication updates categorized properties.
    /// </summary>
    [Fact]
    public async Task UpdateCategorizedInstruction_WithAuthentication_UpdatesInstruction()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.write" });

        // First create an instruction
        var createRequest = new CreateSystemInstructionRequest
        {
            Name = "Topic Instruction",
            Category = SystemInstructionCategory.Topic,
            TopicKey = "Original-Topic",
            Priority = 5,
            PersonaDefinition = "Original persona",
            BusinessConstraints = "Original constraints",
            IsActive = true
        };
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);

        var updateRequest = new UpdateSystemInstructionRequest
        {
            TopicKey = "Updated-Topic",
            Priority = 20
        };

        // Act
        Assert.NotNull(created);
        var response = await client.PutAsJsonAsync($"/chatbot/v1/admin/instructions/{created.Id}", updateRequest, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated-Topic", updated.TopicKey);
        Assert.Equal(20, updated.Priority);
    }

    /// <summary>
    /// Tests that POST /v1/admin/instructions/refine uses the LLM to improve an instruction draft.
    /// </summary>
    [Fact]
    public async Task RefineInstruction_WithAuthentication_ReturnsImprovedDraft()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.write" });
        var request = new RefineSystemInstructionRequest
        {
            Name = "Customer Website Assistant",
            Category = SystemInstructionCategory.Core,
            TopicKey = "website",
            PersonaDefinition = "Mali answers website questions.",
            BusinessConstraints = "Keep customer data safe."
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions/refine", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refined = await response.Content.ReadFromJsonAsync<RefinedSystemInstructionResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(refined);
        Assert.Contains("Refined", refined.PersonaDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer-safe", refined.BusinessConstraints, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(refined.Summary));
    }

    /// <summary>
    /// Tests that POST /v1/admin/instructions/refine requires write permission.
    /// </summary>
    [Fact]
    public async Task RefineInstruction_WithoutPermission_Returns403()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.read" });
        var request = new RefineSystemInstructionRequest
        {
            Name = "Customer Website Assistant",
            Category = SystemInstructionCategory.Core,
            TopicKey = "website",
            PersonaDefinition = "Mali answers website questions.",
            BusinessConstraints = "Keep customer data safe."
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions/refine", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that DELETE /v1/admin/instructions/{id} with authentication deactivates instruction.
    /// </summary>
    [Fact]
    public async Task DeleteInstruction_WithAuthentication_DeactivatesInstruction()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.write" });

        // First create an instruction
        var createRequest = new CreateSystemInstructionRequest
        {
            Name = "To Be Deleted",
            PersonaDefinition = "This is a test persona definition for deletion test",
            BusinessConstraints = "This is a test business constraint for deletion test",
            IsActive = true
        };
        var createResponse = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions", createRequest, _factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<SystemInstructionDto>(_factory.JsonSerializerOptions);

        // Act
        Assert.NotNull(created);
        var response = await client.DeleteAsync($"/chatbot/v1/admin/instructions/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Tests that Admin API requires chatbot.instructions.read permission.
    /// </summary>
    [Fact]
    public async Task GetInstructions_WithoutPermission_Returns403()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "some.other.permission" });

        // Act
        var response = await client.GetAsync("/chatbot/v1/admin/instructions");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that Admin API requires chatbot.instructions.write permission for mutations.
    /// </summary>
    [Fact]
    public async Task CreateInstruction_WithoutPermission_Returns403()
    {
        // Arrange
        var client = CreateAuthenticatedClient(new[] { "chatbot.instructions.read" }); // Only read permission

        var request = new CreateSystemInstructionRequest
        {
            Name = "Unauthorized",
            PersonaDefinition = "This is a long enough persona definition",
            BusinessConstraints = "This is a long enough business constraint",
            IsActive = false
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/admin/instructions", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

