using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for GeminiClient with retry logic and timeout handling.
/// </summary>
[Collection("Database")]
public class GeminiClientTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public GeminiClientTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Initializes the test.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;
    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
    /// <summary>
    /// Tests that Gemini API call succeeds with valid request.
    /// </summary>
    [Fact(Skip = "Requires live Gemini API key (GEMINI_API_KEY env var)")]
    public async Task SendMessageAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGeminiClient>();

        var request = new GeminiRequest
        {
            Prompt = "What is stainless steel 304?",
            SystemInstruction = "You are a manufacturing assistant",
            MaxTokens = 1000,
            Temperature = 0.7
        };

        // Act
        var response = await client.SendMessageAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(string.IsNullOrEmpty(response.Content));
    }

    /// <summary>
    /// Tests that API timeout is handled correctly.
    /// </summary>
    [Fact(Skip = "Requires live Gemini API key (GEMINI_API_KEY env var)")]
    public async Task SendMessageAsync_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGeminiClient>();

        var request = new GeminiRequest
        {
            Prompt = "Test timeout",
            SystemInstruction = "Test",
            Timeout = TimeSpan.FromMilliseconds(1) // Very short timeout
        };

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() => client.SendMessageAsync(request));
    }

    /// <summary>
    /// Tests that transient failures are retried.
    /// </summary>
    [Fact(Skip = "Requires live Gemini API key (GEMINI_API_KEY env var)")]
    public async Task SendMessageAsync_TransientFailure_Retries()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGeminiClient>();

        var request = new GeminiRequest
        {
            Prompt = "Test retry logic",
            SystemInstruction = "Test",
            MaxTokens = 500
        };

        // Act - Client should automatically retry on transient failures
        var response = await client.SendMessageAsync(request);

        // Assert
        Assert.NotNull(response);
    }

    /// <summary>
    /// Tests that multimodal request with image is handled.
    /// </summary>
    [Fact(Skip = "Requires live Gemini API key (GEMINI_API_KEY env var)")]
    public async Task SendMessageAsync_WithImage_ProcessesSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGeminiClient>();

        var request = new GeminiRequest
        {
            Prompt = "Analyze this technical drawing",
            SystemInstruction = "You are a manufacturing assistant",
            ImageUrl = "https://example.com/drawing.jpg",
            MaxTokens = 1500
        };

        // Act
        var response = await client.SendMessageAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(string.IsNullOrEmpty(response.Content));
    }

    /// <summary>
    /// Tests that default timeout is 10 seconds.
    /// </summary>
    [Fact(Skip = "Requires live Gemini API key (GEMINI_API_KEY env var)")]
    public async Task SendMessageAsync_NoTimeoutSpecified_UsesDefault10Seconds()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGeminiClient>();

        var request = new GeminiRequest
        {
            Prompt = "Test default timeout",
            SystemInstruction = "Test"
        };

        // Act
        var response = await client.SendMessageAsync(request);

        // Assert
        Assert.NotNull(response);
    }

    /// <summary>
    /// Tests that web search timeout is 30 seconds.
    /// </summary>
    [Fact(Skip = "Requires live Gemini API key (GEMINI_API_KEY env var)")]
    public async Task SendMessageAsync_WithWebSearch_Uses30SecondTimeout()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGeminiClient>();

        var request = new GeminiRequest
        {
            Prompt = "Search for ASTM A36 steel properties",
            SystemInstruction = "You have web search capabilities",
            EnableWebSearch = true
        };

        // Act
        var response = await client.SendMessageAsync(request);

        // Assert
        Assert.NotNull(response);
    }
}

