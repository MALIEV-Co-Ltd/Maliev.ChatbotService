using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using System.Net;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for the HealthController liveness and readiness endpoints.
/// </summary>
[Collection("Database")]
public class HealthEndpointTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    public HealthEndpointTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetLiveness_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/chatbot/liveness");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReadiness_ReturnsOkOrServiceUnavailable()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/chatbot/readiness");
        // Returns 200 if DB is up, 503 if not
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }
}
