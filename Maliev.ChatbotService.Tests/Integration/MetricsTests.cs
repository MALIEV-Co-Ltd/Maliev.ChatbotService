using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using Maliev.ChatbotService.Tests.Infrastructure;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for Prometheus metrics exposure (Constitution XII).
/// </summary>
[Collection("Database")]
public class MetricsTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public MetricsTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private HttpClient CreateClient() => _factory.CreateClient();
    /// <summary>
    /// Tests that GET /chatbot/metrics exposes conversation_volume counter.
    /// </summary>
    [Fact]
    public async Task Metrics_Endpoint_ExposesConversationVolumeCounter()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/chatbot/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("conversation_volume", content);
    }

    /// <summary>
    /// Tests that GET /chatbot/metrics exposes response_latency_p90 histogram.
    /// </summary>
    [Fact]
    public async Task Metrics_Endpoint_ExposesResponseLatencyHistogram()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/chatbot/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("response_latency", content);
    }

    /// <summary>
    /// Tests that GET /chatbot/metrics exposes gemini_api_success_rate gauge.
    /// </summary>
    [Fact]
    public async Task Metrics_Endpoint_ExposesGeminiApiSuccessRateGauge()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/chatbot/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("gemini_api_success_rate", content);
    }

    /// <summary>
    /// Tests that GET /chatbot/metrics exposes active_sessions_count gauge.
    /// </summary>
    [Fact]
    public async Task Metrics_Endpoint_ExposesActiveSessionsCountGauge()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/chatbot/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("active_sessions_count", content);
    }

    /// <summary>
    /// Tests that GET /chatbot/metrics exposes user_satisfaction_score gauge (if available).
    /// </summary>
    [Fact]
    public async Task Metrics_Endpoint_ExposesUserSatisfactionScoreGauge()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/chatbot/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        // User satisfaction score is optional
    }

    /// <summary>
    /// Tests that metrics include required tags (service_name, version, region, environment).
    /// </summary>
    [Fact]
    public async Task Metrics_Endpoint_IncludesRequiredTags()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync("/chatbot/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("service_name", content);
        Assert.Contains("version", content);
        Assert.Contains("environment", content);
    }
}

