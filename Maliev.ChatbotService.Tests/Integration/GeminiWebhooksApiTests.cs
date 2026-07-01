using System.Net;
using System.Security.Cryptography;
using System.Text;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.BackgroundServices;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Maliev.ChatbotService.Tests.Integration;

public sealed class GeminiWebhooksApiTests : IAsyncLifetime
{
    private const string SigningSecret = "whsec_dGVzdC13ZWJob29rLXNlY3JldA==";
    private readonly GeminiWebhookTestFactory _factory = new();

    public async Task InitializeAsync()
    {
        RecordingConversationSummaryBatchService.Reset();
        await _factory.InitializeAsync();
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        Environment.SetEnvironmentVariable("Gemini__Webhooks__SigningSecret", null);
    }

    [Fact]
    public async Task POST_GeminiWebhook_WithValidBatchEvent_ProcessesOpenBatches()
    {
        var client = _factory.CreateClient();
        const string payload = """
            {"type":"batch.succeeded","version":"v1","timestamp":"2026-07-01T00:00:00Z","data":{"id":"batches/test-batch","output_file_uri":"gs://maliev/test-batch-output.jsonl"}}
            """;

        var request = CreateSignedRequest(payload);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _ = await TestHelpers.WaitForAsync(
            () => Task.FromResult(RecordingConversationSummaryBatchService.ProcessOpenBatchCallCount),
            count => count == 1,
            timeout: TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(100),
            message: "Gemini batch webhook was not processed.");
    }

    [Fact]
    public async Task POST_GeminiWebhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        const string payload = """
            {"type":"batch.succeeded","version":"v1","timestamp":"2026-07-01T00:00:00Z","data":{"id":"batches/test-batch"}}
            """;

        using var request = CreateSignedRequest(payload);
        request.Headers.Remove("webhook-signature");
        request.Headers.TryAddWithoutValidation("webhook-signature", "v1,invalid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, RecordingConversationSummaryBatchService.ProcessOpenBatchCallCount);
    }

    private static HttpRequestMessage CreateSignedRequest(string payload)
    {
        var webhookId = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/chatbot/v1/webhooks/gemini")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("webhook-id", webhookId);
        request.Headers.TryAddWithoutValidation("webhook-timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("webhook-signature", Sign(webhookId, timestamp, payload));

        return request;
    }

    private static string Sign(string webhookId, string timestamp, string payload)
    {
        var secretBytes = Convert.FromBase64String(SigningSecret["whsec_".Length..]);
        var signedContent = $"{webhookId}.{timestamp}.{payload}";
        using var hmac = new HMACSHA256(secretBytes);
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)));

        return $"v1,{signature}";
    }

    private sealed class GeminiWebhookTestFactory : BaseIntegrationTestFactory<Program, ChatbotDbContext>
    {
        protected override void ConfigureEnvironmentVariables()
        {
            base.ConfigureEnvironmentVariables();
            Environment.SetEnvironmentVariable("Gemini__Webhooks__SigningSecret", SigningSecret);
        }

        protected override void ConfigureAdditionalServices(IServiceCollection services)
        {
            base.ConfigureAdditionalServices(services);
            foreach (var descriptor in services
                         .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                             && descriptor.ImplementationType == typeof(SessionExpiryBackgroundService))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IConversationSummaryBatchService>();
            services.AddScoped<IConversationSummaryBatchService, RecordingConversationSummaryBatchService>();
        }
    }

    private sealed class RecordingConversationSummaryBatchService : IConversationSummaryBatchService
    {
        public static int ProcessOpenBatchCallCount;

        public static void Reset()
        {
            Interlocked.Exchange(ref ProcessOpenBatchCallCount, 0);
        }

        public Task<HashSet<Guid>> SubmitExpiredSessionSummariesAsync(
            IReadOnlyCollection<ConversationSession> sessions,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new HashSet<Guid>());

        public Task ProcessOpenBatchesAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ProcessOpenBatchCallCount);
            return Task.CompletedTask;
        }
    }
}
