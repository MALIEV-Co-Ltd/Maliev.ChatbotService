using System.Net;
using System.Net.Http;
using System.Threading.RateLimiting;
using Maliev.ChatbotService.Api.RateLimiting;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Validators;
using Maliev.ChatbotService.Infrastructure.AI;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Infrastructure.ExternalServices;
using Maliev.ChatbotService.Infrastructure.Messaging;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Maliev.ChatbotService.Infrastructure.Repositories;
using Maliev.ChatbotService.Infrastructure.Services;
using Maliev.ChatbotService.Infrastructure.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Program.Log.StartingHost(bootstrapLogger, "Chatbot Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume();

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults();
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });
    builder.AddServiceMeters("chatbot-meter");

    var isIntegrationTest = builder.Environment.EnvironmentName == "Testing";

    // --- Data & Cache ---
    builder.AddPostgresDbContext<ChatbotDbContext>("ChatbotDbContext", enableDynamicJson: true);
    builder.AddStandardCache("chatbot:"); // Redis + in-memory fallback, memory-optimized
    var redisConnectionString = builder.Configuration.GetConnectionString("redis");
    if (isIntegrationTest && !string.IsNullOrWhiteSpace(redisConnectionString))
    {
        builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 10;
            redisOptions.ConnectTimeout = 60_000;
            redisOptions.SyncTimeout = 60_000;
            redisOptions.AsyncTimeout = 60_000;
            redisOptions.ReconnectRetryPolicy = new ExponentialRetry(1_000, 10_000);

            return ConnectionMultiplexer.Connect(redisOptions);
        });
    }

    // --- Messaging ---
    builder.AddMassTransitWithRabbitMq(cfg =>
    {
        // Configure consumers here when messaging events are implemented
        // Example: cfg.AddConsumer<SomeEventConsumer>();
    });

    // --- Security ---
    builder.AddJwtAuthentication();

    // --- API Configuration ---
    builder.AddStandardCors(); // CORS with fail-fast validation
    builder.AddDefaultApiVersioning();

    // Per-IP rate limit on message sends, independent of UserProfileId, so an anonymous caller
    // cannot reset their budget by initiating a fresh session (S1). Production default is 60/min
    // (override via RateLimiting:MessagesPerIpPerMinute, e.g. an env var); disabled (0) under
    // integration tests unless a test sets the value explicitly.
    var messagesPerIpPerMinute = builder.Configuration.GetValue<int?>("RateLimiting:MessagesPerIpPerMinute")
        ?? (isIntegrationTest ? 0 : 60);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = 429;
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers["Retry-After"] = "60";
            return ValueTask.CompletedTask;
        };
        options.AddPolicy(ChatbotRateLimiterPolicies.MessagesPerIp, httpContext =>
        {
            if (messagesPerIpPerMinute <= 0)
            {
                return RateLimitPartition.GetNoLimiter("disabled");
            }

            // Exempt trusted first-party services (e.g. the QuoteEngine BFF), which authenticate with a
            // signed service-account JWT and do their own per-visitor/IP limiting at their public
            // ingress. They are not the anonymous-public-abuse surface this limiter targets, and keying
            // them on their single service IP would collapse every customer behind that BFF into one
            // shared budget. This trusts a validated JWT claim, not a spoofable header; direct public
            // callers (website/webhook) remain IP-limited. Requires UseRateLimiter to run after
            // UseAuthentication so the principal is populated.
            if (httpContext.User.FindFirst("user_type")?.Value == "service")
            {
                return RateLimitPartition.GetNoLimiter("service-account");
            }

            // RemoteIpAddress already reflects the real client IP: UseStandardMiddleware runs
            // ForwardedHeaders (X-Forwarded-For) first, so we do not parse the spoofable header here.
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = messagesPerIpPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        });
    });

    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Chatbot Service API",
            description: "Chatbot service for Maliev platform interactions.");
    }

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
        });

    // --- Application Services ---

    // Repositories
    builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
    builder.Services.AddScoped<IConversationSessionRepository, ConversationSessionRepository>();
    builder.Services.AddScoped<IMessageRepository, MessageRepository>();
    builder.Services.AddScoped<IConversationSummaryRepository, ConversationSummaryRepository>();
    builder.Services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
    builder.Services.AddScoped<ISystemInstructionRepository, SystemInstructionRepository>();
    builder.Services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
    builder.Services.AddScoped<IIdentityLinkRepository, IdentityLinkRepository>();
    builder.Services.AddScoped<IOperationLogRepository, OperationLogRepository>();
    builder.Services.AddScoped<ISearchDomainLogRepository, SearchDomainLogRepository>();

    // Services
    builder.Services.AddScoped<ISystemInstructionService, SystemInstructionService>();
    builder.Services.AddScoped<IConversationSummaryService, ConversationSummaryService>();
    builder.Services.AddScoped<IRateLimitService, RateLimitService>();
    builder.Services.AddScoped<IUsageBudgetService, RedisUsageBudgetService>();
    builder.Services.AddSingleton<IWebhookBufferQueue, RedisWebhookBufferQueue>();
    builder.Services.AddScoped<IWebhookBufferProcessor, WebhookBufferProcessor>();
    builder.Services.AddScoped<ILanguageDetectionService, LanguageDetectionService>();
    builder.Services.AddScoped<IResponseFormatterService, ResponseFormatterService>();
    builder.Services.AddScoped<ISessionExpiryService, SessionExpiryService>();
    builder.Services.AddScoped<IIntentClassificationService, IntentClassificationService>();
    builder.Services.AddScoped<IEventPublisher, EventPublisher>();
    builder.Services.AddScoped<IOperationExecutionService, OperationExecutionService>();
    builder.Services.AddScoped<IFileValidationService, FileValidationService>();
    builder.Services.AddScoped<IExtractPreferencesService, ExtractPreferencesService>();
    builder.Services.AddScoped<BusinessConstraintValidator>();
    builder.Services.AddSingleton<IConversationMetrics, ConversationMetrics>();
    builder.Services.AddSingleton<ConversationMetrics>();

    // Background Services
    builder.Services.AddHostedService<Maliev.ChatbotService.Infrastructure.BackgroundServices.SessionExpiryBackgroundService>();
    builder.Services.AddHostedService<Maliev.ChatbotService.Infrastructure.Services.PromptFileLoaderService>();

    // The webhook buffer poller is disabled under integration tests (a 1s loop racing the per-test
    // Redis flush would intermittently fail unrelated tests); the queue + processor stay registered so
    // tests can drive them deterministically.
    if (!isIntegrationTest)
    {
        builder.Services.AddHostedService<Maliev.ChatbotService.Infrastructure.BackgroundServices.WebhookBufferProcessorBackgroundService>();
    }

    // IAM Registration (skip in integration tests to avoid service discovery delays)
    if (!isIntegrationTest)
    {
        builder.AddIAMServiceClient("chatbot");
        builder.Services.AddIAMRegistration<ChatbotIAMRegistrationService>("chatbot");
    }

    // Handlers
    builder.Services.AddScoped<InitiateSessionCommandHandler>();
    builder.Services.AddScoped<SendMessageCommandHandler>();
    builder.Services.AddScoped<LinkIdentityCommandHandler>();
    builder.Services.AddScoped<GetConversationHistoryQueryHandler>();
    builder.Services.AddScoped<GetUserPreferencesQueryHandler>();
    builder.Services.AddScoped<DeleteUserDataCommandHandler>();
    builder.Services.AddScoped<CreateSystemInstructionCommandHandler>();
    builder.Services.AddScoped<UpdateSystemInstructionCommandHandler>();
    builder.Services.AddScoped<GetSystemInstructionsQueryHandler>();
    builder.Services.AddScoped<RefineSystemInstructionCommandHandler>();
    builder.Services.AddScoped<ProcessWebhookCommandHandler>();
    builder.Services.AddScoped<ExtractCustomerCommandHandler>();
    builder.Services.AddScoped<ExtractCustomerIntentCommandHandler>();
    builder.Services.AddScoped<CleanDictationSpeechCommandHandler>();
    builder.Services.AddScoped<AgentChatHandler>();

    // Service Clients
    builder.AddServiceClient<IIAMServiceClient, IAMServiceClient>("IAM");

    // External Clients
    var externalClientsConfig = builder.Configuration.GetSection("ExternalClients").Get<ExternalClientsConfiguration>()
        ?? new ExternalClientsConfiguration();

    if (isIntegrationTest)
    {
        builder.Services.AddScoped<IGeminiClient, TestingGeminiClient>();
        builder.Services.AddScoped<IModelContextCacheService, NoOpModelContextCacheService>();
        builder.Services.AddScoped<IModelBatchClient, NoOpModelBatchClient>();
    }
    else
    {
        builder.Services.AddScoped<IModelProviderClientFactory, ModelProviderClientFactory>();
        builder.Services.AddScoped<IGeminiClient, ProviderRoutingGeminiClient>();

        if (Program.UsesNativeGeminiProvider(builder.Configuration["Llm:Provider"]))
        {
            builder.Services.AddHttpClient<IModelContextCacheService, GeminiModelContextCacheService>(client =>
            {
                client.BaseAddress = new Uri(externalClientsConfig.Gemini.BaseAddress);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.ShouldHandle = args => Program.ShouldRetryModelProviderFailure(
                    args.Outcome.Result,
                    args.Outcome.Exception);
            });

            builder.Services.AddHttpClient<IModelBatchClient, GeminiBatchClient>(client =>
            {
                client.BaseAddress = new Uri(externalClientsConfig.Gemini.BaseAddress);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.ShouldHandle = args => Program.ShouldRetryModelProviderFailure(
                    args.Outcome.Result,
                    args.Outcome.Exception);
            });
        }
        else
        {
            builder.Services.AddScoped<IModelContextCacheService, NoOpModelContextCacheService>();
            builder.Services.AddScoped<IModelBatchClient, NoOpModelBatchClient>();
        }

        builder.Services.AddHttpClient<GeminiModelProviderClient>(client =>
        {
            client.BaseAddress = new Uri(externalClientsConfig.Gemini.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddStandardResilienceHandler(options =>
        {
            // GeminiClient already handles 429 (TooManyRequests) internally by returning a
            // graceful fallback response. Polly retrying 429 with exponential backoff adds
            // ~30s of delay before GeminiClient ever sees the response.
            options.Retry.ShouldHandle = args => Program.ShouldRetryModelProviderFailure(
                args.Outcome.Result,
                args.Outcome.Exception);
        });

        builder.Services.AddHttpClient<OpenAICompatibleModelProviderClient>(client =>
        {
            client.BaseAddress = new Uri(externalClientsConfig.OpenAICompatible.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.ShouldHandle = args => Program.ShouldRetryModelProviderFailure(
                args.Outcome.Result,
                args.Outcome.Exception);
        });
    }

    builder.Services.AddHttpClient<IWebSearchService, WebSearchService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<ILineClient, LineClient>(client =>
    {
        client.BaseAddress = new Uri(externalClientsConfig.Line.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IMetaClient, MetaClient>(client =>
    {
        client.BaseAddress = new Uri(externalClientsConfig.Facebook.BaseAddress);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

    // Tool Executor for AI agent function calling
    builder.Services.AddSingleton<IToolExecutorService, ToolExecutorService>();

    // Named HttpClients for microservice integration
    builder.AddServiceClient("QuotationService");
    builder.AddServiceClient("OrderService");
    builder.AddServiceClient("CustomerService");
    builder.AddServiceClient("InvoiceService");
    builder.AddServiceClient("PaymentService");
    builder.AddServiceClient("EmployeeService");
    builder.AddServiceClient("MaterialService");
    builder.AddServiceClient("SupplierService");
    builder.AddServiceClient("ReceiptService");
    builder.AddServiceClient("UploadService");
    builder.AddServiceClient("CareerService");
    builder.AddServiceClient("QuoteEngineBff");

    // HttpClient for thinking step callbacks to BFF
    builder.Services.AddHttpClient("ThinkingCallback")
        .AddServiceDiscovery()
        .AddStandardResilienceHandler();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    // --- Database Migrations ---
    await app.MigrateDatabaseAsync<ChatbotDbContext>();

    // --- Middleware Pipeline ---
    app.UseStandardMiddleware();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseRouting();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    // After authentication so the rate-limiter partitioner can read the principal and exempt trusted
    // service accounts (see the MessagesPerIp policy). Still after UseRouting, so endpoint policies resolve.
    app.UseRateLimiter();

    app.MapControllers();
    app.MapDefaultEndpoints("chatbot");
    app.MapApiDocumentation("chatbot");

    Program.Log.ServiceStarted(logger, "Chatbot Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Program.Log.HostTerminated(bootstrapLogger, ex, "Chatbot Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Program class for integration testing.
/// </summary>
public partial class Program
{
    private static bool UsesNativeGeminiProvider(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return true;
        }

        return providerName.Trim().ToLowerInvariant() switch
        {
            "gemini" => true,
            "google" => true,
            "google-gemini" => true,
            _ => false
        };
    }

    private static ValueTask<bool> ShouldRetryModelProviderFailure(HttpResponseMessage? response, Exception? exception)
    {
        if (response?.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return ValueTask.FromResult(false);
        }

        if (response is { IsSuccessStatusCode: false } failedResponse &&
            ((int)failedResponse.StatusCode >= 500 || failedResponse.StatusCode == HttpStatusCode.RequestTimeout))
        {
            return ValueTask.FromResult(true);
        }

        if (exception is HttpRequestException)
        {
            return ValueTask.FromResult(true);
        }

        return ValueTask.FromResult(false);
    }

    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);
    }
}
