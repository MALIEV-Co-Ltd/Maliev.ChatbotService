using Maliev.Aspire.ServiceDefaults;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Validators;
using Maliev.ChatbotService.Infrastructure.AI;
using Maliev.ChatbotService.Infrastructure.BackgroundServices;
using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Infrastructure.ExternalServices;
using Maliev.ChatbotService.Infrastructure.Messaging;
using Maliev.ChatbotService.Infrastructure.Metrics;
using Maliev.ChatbotService.Infrastructure.Repositories;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

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
builder.AddRedisDistributedCache("redis");
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis") ?? "localhost"));

// --- Messaging ---
builder.AddMassTransitWithRabbitMq(cfg =>
{
    // Configure consumers here when messaging events are implemented
    // Example: cfg.AddConsumer<SomeEventConsumer>();
});

// --- Security ---
builder.AddJwtAuthentication();

// --- API Configuration ---
builder.AddDefaultCors();
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
// ENFORCED PATTERN: Services:{ServiceName}:BaseUrl (no fallbacks)
builder.Services.AddHttpClient("QuotationService", client =>
{
    var baseUrl = builder.Configuration["Services:QuotationService:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Required configuration 'Services:QuotationService:BaseUrl' is missing. Check appsettings.json or environment variables.");
    client.BaseAddress = new Uri(baseUrl);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient("OrderService", client =>
{
    var baseUrl = builder.Configuration["Services:OrderService:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Required configuration 'Services:OrderService:BaseUrl' is missing. Check appsettings.json or environment variables.");
    client.BaseAddress = new Uri(baseUrl);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient("CustomerService", client =>
{
    var baseUrl = builder.Configuration["Services:CustomerService:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Required configuration 'Services:CustomerService:BaseUrl' is missing. Check appsettings.json or environment variables.");
    client.BaseAddress = new Uri(baseUrl);
}).AddStandardResilienceHandler();

var app = builder.Build();

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

app.Run();

/// <summary>
/// Program class for integration testing.
/// </summary>
public partial class Program { }
