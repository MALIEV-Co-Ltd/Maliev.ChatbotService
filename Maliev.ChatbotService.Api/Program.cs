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
using Microsoft.EntityFrameworkCore;

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

    // --- Data & Cache ---
    builder.AddPostgresDbContext<ChatbotDbContext>("ChatbotDbContext", enableDynamicJson: true);
    builder.AddStandardCache("chatbot:"); // Redis + in-memory fallback, memory-optimized (includes IConnectionMultiplexer)

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
    builder.Services.AddScoped<IInputValidationService, InputValidationService>();
    builder.Services.AddScoped<IResponseTimeoutService, ResponseTimeoutService>();
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

    // IAM Registration
    builder.AddIAMServiceClient("chatbot");
    builder.Services.AddIAMRegistration<ChatbotIAMRegistrationService>("chatbot");

    // Handlers
    builder.Services.AddScoped<InitiateSessionCommandHandler>();
    builder.Services.AddScoped<SendMessageCommandHandler>();
    builder.Services.AddScoped<LinkIdentityCommandHandler>();
    builder.Services.AddScoped<GetUserPreferencesQueryHandler>();
    builder.Services.AddScoped<DeleteUserDataCommandHandler>();
    builder.Services.AddScoped<CreateSystemInstructionCommandHandler>();
    builder.Services.AddScoped<UpdateSystemInstructionCommandHandler>();
    builder.Services.AddScoped<GetSystemInstructionsQueryHandler>();
    builder.Services.AddScoped<ProcessWebhookCommandHandler>();
    builder.Services.AddScoped<ExtractCustomerCommandHandler>();

    // Service Clients
    builder.AddServiceClient<IIAMServiceClient, IAMServiceClient>("IAM");

    // External Clients
    builder.Services.AddHttpClient<IGeminiClient, GeminiClient>(client =>
    {
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IWebSearchService, WebSearchService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<ILineClient, LineClient>(client =>
    {
        client.BaseAddress = new Uri("https://api.line.me/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

    builder.Services.AddHttpClient<IMetaClient, MetaClient>(client =>
    {
        client.BaseAddress = new Uri("https://graph.facebook.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

    // Named HttpClients for OperationExecutionService
    builder.AddServiceClient("QuotationService");
    builder.AddServiceClient("OrderService");
    builder.AddServiceClient("CustomerService");

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
