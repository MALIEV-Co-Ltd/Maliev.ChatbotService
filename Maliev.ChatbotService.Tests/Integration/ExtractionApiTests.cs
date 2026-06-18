using Maliev.ChatbotService.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Tests.Infrastructure;
using Xunit;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Integration tests for Extraction API endpoints.
/// </summary>
[Collection("Database")]
public class ExtractionApiTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionApiTests"/> class.
    /// </summary>
    /// <param name="factory">The test factory.</param>
    public ExtractionApiTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
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

    private HttpClient CreateAuthenticatedClient() => _factory.CreateAuthenticatedClient();

    /// <summary>
    /// Tests that ExtractCustomer with raw text returns extracted data.
    /// </summary>
    [Fact]
    public async Task ExtractCustomer_WithText_ReturnsExtractedData()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var request = new ExtractCustomerRequest
        {
            RawText = "John Doe from Acme Corp, email john@example.com"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/customer", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExtractCustomerResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.Equal("John", result?.FirstName);
    }

    /// <summary>
    /// Tests that ExtractCustomer requires authentication.
    /// </summary>
    [Fact]
    public async Task ExtractCustomer_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new ExtractCustomerRequest
        {
            RawText = "John Doe from Acme Corp"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/customer", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that ExtractCustomerIntent returns detected intent.
    /// </summary>
    [Fact]
    public async Task ExtractCustomerIntent_ReturnsIntent()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var request = new ExtractCustomerIntentRequest
        {
            UserMessage = "Find Acme Corp contact info"
        };

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/customer-intent", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExtractCustomerIntentResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.True(result?.NeedsCustomerData);
    }

    /// <summary>
    /// Tests that ExtractCustomerIntent requires authentication.
    /// </summary>
    [Fact]
    public async Task ExtractCustomerIntent_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new ExtractCustomerIntentRequest
        {
            UserMessage = "Find Acme Corp contact info"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/customer-intent", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that ExtractCustomer with no input returns 400.
    /// </summary>
    [Fact]
    public async Task ExtractCustomer_NoInput_Returns400()
    {
        // Arrange
        var client = CreateAuthenticatedClient();
        var request = new ExtractCustomerRequest();

        // Act
        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/customer", request, _factory.JsonSerializerOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that ExtractCustomer rejects unbounded file batches.
    /// </summary>
    [Fact]
    public async Task ExtractCustomer_TooManyFiles_Returns400()
    {
        var client = CreateAuthenticatedClient();
        var request = new ExtractCustomerRequest
        {
            Files = Enumerable.Range(0, 6)
                .Select(index => new ExtractionFileData
                {
                    FileName = $"file-{index}.txt",
                    MimeType = "text/plain",
                    Base64Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("data"))
                })
                .ToList()
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/customer", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that CleanSpeech returns cleaned text.
    /// </summary>
    [Fact]
    public async Task CleanSpeech_WithText_ReturnsCleanedText()
    {
        var client = CreateAuthenticatedClient();
        var request = new CleanDictationSpeechRequest
        {
            Speech = "um hello test hello test",
            Language = "en"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/clean-speech", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CleanDictationSpeechResponse>(_factory.JsonSerializerOptions);
        Assert.NotNull(result);
        Assert.NotNull(result?.CleanedText);
    }

    /// <summary>
    /// Tests that CleanSpeech requires authentication.
    /// </summary>
    [Fact]
    public async Task CleanSpeech_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new CleanDictationSpeechRequest
        {
            Speech = "hello test",
            Language = "en"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/clean-speech", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that CleanSpeech rejects empty speech text.
    /// </summary>
    [Fact]
    public async Task CleanSpeech_EmptySpeech_Returns400()
    {
        var client = CreateAuthenticatedClient();
        var request = new CleanDictationSpeechRequest
        {
            Speech = "",
            Language = "en"
        };

        var response = await client.PostAsJsonAsync("/chatbot/v1/extraction/clean-speech", request, _factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
