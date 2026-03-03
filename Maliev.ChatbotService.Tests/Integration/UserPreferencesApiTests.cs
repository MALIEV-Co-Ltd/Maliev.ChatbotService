using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Tests.Integration;

[Collection("Database")]
public class UserPreferencesApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;
    private static readonly string TestUserId = Guid.NewGuid().ToString();

    public UserPreferencesApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetMyPreferences_WithAuthentication_ReturnsPreferences()
    {
        var client = _factory.CreateAuthenticatedClient(new[] { "chatbot.preferences.read" }, TestUserId);

        var response = await client.GetAsync("/chatbot/v1/users/me/preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedPreferencesResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetMyPreferences_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/chatbot/v1/users/me/preferences");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyPreferences_WithPagination_ReturnsPaginatedResults()
    {
        var client = _factory.CreateAuthenticatedClient(new[] { "chatbot.preferences.read" }, TestUserId);

        var response = await client.GetAsync("/chatbot/v1/users/me/preferences?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedPreferencesResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.Meta.Page);
        Assert.Equal(10, result.Meta.PageSize);
    }

    [Fact]
    public async Task DeleteMyData_WithoutConfirm_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(new[] { "chatbot.preferences.delete" }, TestUserId);

        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=preferences&confirm=false");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMyData_WithoutAuthentication_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=preferences&confirm=true");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMyData_WithInvalidScope_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(new[] { "chatbot.preferences.delete" }, TestUserId);

        var response = await client.DeleteAsync("/chatbot/v1/users/me/data?scope=invalid&confirm=true");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
