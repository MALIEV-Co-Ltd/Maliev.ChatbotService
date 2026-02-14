using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Infrastructure.Data;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

// Disable parallel execution to prevent race conditions on the shared singleton database
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Maliev.ChatbotService.Tests.Infrastructure;

/// <summary>
/// Base integration test factory for ChatbotService.
/// Provides PostgreSQL, Redis, and RabbitMQ containers with parallel startup.
/// </summary>
/// <typeparam name="TProgram">The Program class of the service being tested</typeparam>
/// <typeparam name="TDbContext">The DbContext type for the service</typeparam>
public class BaseIntegrationTestFactory<TProgram, TDbContext> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
    where TDbContext : DbContext
{
    private static PostgreSqlContainer? _postgresContainer;
    private static RedisContainer? _redisContainer;
    private static RabbitMqContainer? _rabbitmqContainer;
    private static bool _containersStarted;
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    
    private readonly RSA _testRsa;

    /// <summary>
    /// Override this property if your DbContext connection string has a different name.
    /// Defaults to the DbContext class name.
    /// </summary>
    protected virtual string DbConnectionStringName => typeof(TDbContext).Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseIntegrationTestFactory{TProgram, TDbContext}"/> class.
    /// </summary>
    public BaseIntegrationTestFactory()
    {
        _testRsa = RSA.Create(2048);

        // Set environment variable EARLY so Program.cs picks it up during WebApplication.CreateBuilder
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    /// <summary>
    /// Initializes the test factory by starting all containers and applying database migrations.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try 
        {
            if (!_containersStarted)
            {
                _postgresContainer = new PostgreSqlBuilder("postgres:18-alpine")
                    .WithCommand("-c", "max_connections=1000")
                    .Build();

                _redisContainer = new RedisBuilder("redis:7.4-alpine")
                    .WithCommand("redis-server", "--requirepass", "", "--protected-mode", "no")
                    .Build();

                _rabbitmqContainer = new RabbitMqBuilder("rabbitmq:4.0-alpine")
                    .WithUsername("guest")
                    .WithPassword("guest")
                    .Build();

                // Start all containers in parallel
                await Task.WhenAll(
                    _postgresContainer.StartAsync(),
                    _redisContainer.StartAsync(),
                    _rabbitmqContainer.StartAsync()
                );
                
                // Ensure PostgreSQL is fully ready and accepting connections
                var postgresReady = false;
                var retryCount = 0;
                const int maxRetries = 60; 
                while (!postgresReady && retryCount < maxRetries)
                {
                    try
                    {
                        await using var conn = new Npgsql.NpgsqlConnection(_postgresContainer.GetConnectionString());
                        await conn.OpenAsync();
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1";
                        await cmd.ExecuteScalarAsync();
                        postgresReady = true;
                    }
                    catch
                    {
                        retryCount++;
                        await Task.Delay(1000);
                    }
                }

                if (!postgresReady)
                {
                    throw new InvalidOperationException("PostgreSQL Testcontainer failed to become ready (Ping failed) after 60 seconds.");
                }

                // Wait for Redis to be ready
                using (var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString()))
                {
                    await connection.GetDatabase().PingAsync();
                }

                // Apply database migrations
                await ApplyMigrationsAsync();

                _containersStarted = true;
            }
        }
        finally
        {
            _initLock.Release();
        }

        var postgresConnectionString = _postgresContainer!.GetConnectionString();
        var redisConnectionString = _redisContainer!.GetConnectionString();
        var rabbitMqConnectionString = _rabbitmqContainer!.GetConnectionString();

        // Set environment variables immediately after containers start
        Environment.SetEnvironmentVariable($"ConnectionStrings__{DbConnectionStringName}", postgresConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__redis", redisConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", rabbitMqConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__messaging", rabbitMqConnectionString);
    }

    /// <summary>
    /// Disposes all test resources including containers and RSA keys.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync(); // Stop the Host
        // Static containers are NOT disposed here to allow reuse across tests
        _testRsa.Dispose();
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null); // Cleanup
    }

    /// <summary>
    /// Creates the host for the test factory, ensuring containers are started and JWT keys are configured.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The created host.</returns>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure containers are started before creating host
        if (!_containersStarted)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }

        // Export RSA public key for JWT validation
        var rsaParams = _testRsa.ExportParameters(false);
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY_MODULUS", Convert.ToBase64String(rsaParams.Modulus!));
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY_EXPONENT", Convert.ToBase64String(rsaParams.Exponent!));
        Environment.SetEnvironmentVariable("CORS__AllowedOrigins__0", "http://localhost:3000");
        Environment.SetEnvironmentVariable("CORS__AllowedOrigins__1", "http://localhost:5173");

        // Allow derived classes to set additional environment variables
        ConfigureEnvironmentVariables();

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Configures the web host for testing with test-specific settings and authentication.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Service:Name"] = "ChatbotService",
                ["Service:Version"] = "1.0.0-test",
                // IAM Integration MUST be enabled per Constitution
                // Tests use AllowAllAuthorizationHandler to bypass permission checks
                ["Features:WebSearchEnabled"] = "false",
                // JWT configuration for ServiceAccountTokenProvider in tests
                ["Jwt:SecurityKey"] = "test-secret-key-at-least-32-characters-long",
                // Connection strings for Testcontainers
                [$"ConnectionStrings:{DbConnectionStringName}"] = _postgresContainer!.GetConnectionString(),
                ["ConnectionStrings:redis"] = _redisContainer!.GetConnectionString(),
                ["ConnectionStrings:messaging"] = _rabbitmqContainer!.GetConnectionString(),
                ["CORS:AllowedOrigins:0"] = "http://localhost:3000",
                ["CORS:AllowedOrigins:1"] = "http://localhost:5173",
                ["CORS_ALLOWED_ORIGINS"] = "http://localhost:3000,http://localhost:5173",
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Add startup filter to inject test IP address middleware
            services.AddSingleton<IStartupFilter>(new TestIpAddressStartupFilter());

            // Mock IAM service client to always return true for permission checks
            services.AddScoped<IIAMServiceClient, MockIAMServiceClient>();

            // Mock Gemini client to avoid external API calls
            services.AddScoped<IGeminiClient, MockGeminiClient>();

            // Mock LINE client for webhook tests
            services.AddScoped<ILineClient, MockLineClient>();

            // Mock Meta client for webhook tests
            services.AddScoped<IMetaClient, MockMetaClient>();

            // Mock Web Search Service to avoid external API calls
            services.AddScoped<IWebSearchService, MockWebSearchService>();

            // Mock Operation Execution Service for internal agent tests
            services.AddScoped<IOperationExecutionService, MockOperationExecutionService>();

            // Configure JWT Bearer authentication with test RSA key
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "test-issuer",
                    ValidAudience = "test-audience",
                    IssuerSigningKey = new RsaSecurityKey(_testRsa),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role"
                };
            });

            // Add MassTransit test harness for testing message publishing/consuming
            services.AddMassTransitTestHarness();

            // Allow derived classes to add additional test services
            ConfigureAdditionalServices(services);
        });
    }

    /// <summary>
    /// Override this method to set additional environment variables before host creation.
    /// Called after standard environment variables are set.
    /// </summary>
    protected virtual void ConfigureEnvironmentVariables()
    {
        // Override in derived class if needed
    }

    /// <summary>
    /// Override this method to add additional test services to the DI container.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        var redisConnectionString = _redisContainer!.GetConnectionString();
        
        // Explicitly register Redis for tests because AddStandardCache defaults to In-Memory in Testing env
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
        {
            return StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString);
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "chatbot:";
        });

        services.AddScoped<ICacheService, RedisCacheService>();
    }

    /// <summary>
    /// Gets the DbContext from the service provider for use in tests.
    /// </summary>
    public TDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TDbContext>();
    }

    /// <summary>
    /// Creates a new DbContext instance for testing (not from DI container).
    /// </summary>
    public TDbContext CreateDbContext()
    {
        var connectionString = _postgresContainer!.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    /// <summary>
    /// Applies all pending migrations to the test database.
    /// </summary>
    private async Task ApplyMigrationsAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleans all data from the database while preserving schema.
    /// Queries the database schema dynamically to get all tables.
    /// </summary>
    [SuppressMessage("Security", "EF1002:Gaps in SQL queries", Justification = "Table names are retrieved from information_schema and are safe.")]
    public async Task CleanDatabaseAsync()
    {
        await using var context = CreateDbContext();

        // Get all table names from information_schema
        var tableNames = await context.Database
            .SqlQueryRaw<string>(
                @"SELECT table_name
                  FROM information_schema.tables
                  WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name != '__EFMigrationsHistory'
                  ORDER BY table_name")
            .ToListAsync();

        // Truncate all tables (CASCADE handles foreign keys)
        foreach (var tableName in tableNames)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" RESTART IDENTITY CASCADE");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Table doesn't exist - ignore this error
            }
        }
    }

    /// <summary>
    /// Alias for CleanDatabaseAsync to support different naming conventions.
    /// Also clears Redis cache to ensure clean state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await CleanDatabaseAsync();
        await ClearRedisAsync();
    }

    /// <summary>
    /// Alias for CleanDatabaseAsync to support different naming conventions.
    /// </summary>
    public Task ClearDatabaseAsync() => CleanDatabaseAsync();

    /// <summary>
    /// Clears the in-memory cache.
    /// </summary>
    public void ClearCache()
    {
        // Get IMemoryCache from services and cast to MemoryCache to access Clear()
        var memoryCache = Services.GetService<IMemoryCache>();
        if (memoryCache is MemoryCache cache)
        {
            cache.Compact(1.0); // Compact 100% removes all entries
        }
    }

    /// <summary>
    /// Clears all data from Redis cache.
    /// </summary>
    public async Task ClearRedisAsync()
    {
        try
        {
            var cache = Services.GetRequiredService<IDistributedCache>();

            // Get all keys from Redis and delete them
            // Since IDistributedCache doesn't have a Clear method, we need to flush the Redis database
            // We'll connect directly to Redis using the connection string
            var redisConnectionString = _redisContainer!.GetConnectionString();

            await using var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(redisConnectionString);
            var server = connection.GetServer(connection.GetEndPoints().First());
            await server.FlushDatabaseAsync();
        }
        catch
        {
            // Ignore errors during cache clearing - cache might not be initialized yet
        }
    }

    /// <summary>
    /// Exposes the RSA signing credentials for JWT token creation in tests.
    /// </summary>
    public SigningCredentials SigningCredentials => new SigningCredentials(new RsaSecurityKey(_testRsa), SecurityAlgorithms.RsaSha256);

    /// <summary>
    /// Gets the standard JSON serializer options used by the API.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a test JWT token for authentication in integration tests.
    /// </summary>
    /// <param name="userId">User ID to include in token</param>
    /// <param name="roles">Roles to include in token claims</param>
    /// <param name="additionalClaims">Additional claims to include</param>
    /// <returns>JWT token string</returns>
    public string CreateTestJwtToken(
        string userId = "test-user",
        string[]? roles = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        if (additionalClaims != null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        var rsaSecurityKey = new RsaSecurityKey(_testRsa);
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Simplified JWT token generator with role parameter.
    /// Alias for CreateTestJwtToken to support different naming conventions.
    /// </summary>
    public string GenerateTestToken(string userId = "test-user", string role = "admin")
    {
        return CreateTestJwtToken(userId, new[] { role });
    }

    /// <summary>
    /// Creates an HTTP client with authenticated user and specified roles.
    /// When roles are provided, they are treated as permissions for permission-based authorization.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string userId = "test-user", string[]? roles = null)
    {
        // Create claims list
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId) // Required for PermissionAuthorizationHandler
        };

        // If no roles/permissions specified, add wildcard to bypass all checks
        // If specific permissions provided, add them as "permission" claims for granular testing
        if (roles == null || roles.Length == 0)
        {
            claims.Add(new Claim("permission", "*")); // Wildcard for tests without specific permission requirements
        }
        else
        {
            // Add each role/permission as a separate "permission" claim
            // This allows testing both permission grants and denials
            foreach (var permission in roles)
            {
                claims.Add(new Claim("permission", permission));
            }
        }

        // Create JWT token
        var rsaSecurityKey = new RsaSecurityKey(_testRsa);
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenString}");
        return client;
    }

    /// <summary>
    /// Creates an HTTP client with authenticated user and specified permissions.
    /// </summary>
    /// <param name="permissions">Array of permissions (e.g., "chatbot.instructions.write").</param>
    /// <param name="userId">Optional user ID.</param>
    /// <returns>HTTP client with authentication header.</returns>
    public HttpClient CreateAuthenticatedClient(string[] permissions, string userId = "test-user")
    {
        // Create a JWT token with permissions as claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId), // Add NameIdentifier for PermissionAuthorizationHandler
        };

        // Add wildcard permission for tests to bypass all permission checks
        claims.Add(new Claim("permission", "*"));

        // Add each specific permission as well
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var rsaSecurityKey = new RsaSecurityKey(_testRsa);
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: signingCredentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenString}");
        return client;
    }
}

/// <summary>
/// Startup filter that injects middleware to override RemoteIpAddress with X-Test-Client-IP header.
/// This allows integration tests to simulate requests from specific IP addresses for rate limiting tests.
/// </summary>
internal class TestIpAddressStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> nextFilter)
    {
        return app =>
        {
            app.UseMiddleware<TestIpAddressMiddleware>();
            nextFilter(app);
        };
    }
}

/// <summary>
/// Middleware that overrides RemoteIpAddress with X-Test-Client-IP header value for integration tests.
/// </summary>
internal class TestIpAddressMiddleware
{
    private readonly RequestDelegate _next;

    public TestIpAddressMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Override RemoteIpAddress if X-Test-Client-IP header is present
        if (context.Request.Headers.TryGetValue("X-Test-Client-IP", out var testIp))
        {
            if (System.Net.IPAddress.TryParse(testIp.ToString(), out var ipAddress))
            {
                context.Connection.RemoteIpAddress = ipAddress;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Mock implementation of IAM service client for integration tests.
/// Always returns true for permission checks.
/// </summary>
internal class MockIAMServiceClient : IIAMServiceClient
{
    public Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CheckPermissionAsync(string userId, string permission, string? resourcePath = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string> { "*" });
    }

    public Task RegisterServicePermissionsAsync(string serviceName, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock implementation of Gemini API client for integration tests.
/// Returns canned responses to avoid external API dependencies and costs.
/// </summary>
internal class MockGeminiClient : IGeminiClient
{
    public async Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default)
    {
        // Simulate timeout if requested timeout is very short
        if (request.Timeout.HasValue && request.Timeout.Value.TotalMilliseconds < 10)
        {
            await Task.Delay(100, cancellationToken);
            throw new TimeoutException("The Gemini API request timed out");
        }

        // Get the last user message to determine the appropriate response
        var lastMessage = request.Messages.LastOrDefault()?.Content?.ToLowerInvariant() ?? string.Empty;
        var systemPrompt = request.Prompt?.ToLowerInvariant() ?? string.Empty;

        string content;

        // Support Thai language
        if (lastMessage.Contains("สวัสดี") || systemPrompt.Contains("สวัสดี"))
        {
            content = "สวัสดีครับ นี่คือคำตอบจำลองจาก Gemini API";
        }
        // Handle Intent Classification requests
        else if (request.SystemInstruction.Contains("intent classifier"))
        {
            var intent = "General";
            if (lastMessage.Contains("3d scanning")) intent = "3D-Scanning";
            else if (lastMessage.Contains("pricing") || lastMessage.Contains("price")) intent = "Pricing";
            else if (lastMessage.Contains("cnc")) intent = "CNC-Machining";

            content = JsonSerializer.Serialize(new
            {
                intent = intent,
                confidence = 0.99,
                additionalTopics = new List<string>()
            });
        }
        // Handle Customer Extraction requests
        else if (systemPrompt.Contains("extract customer") || (request.ResponseMimeType == "application/json" && request.SystemInstruction.Contains("Extract customer information")))
        {
            content = JsonSerializer.Serialize(new
            {
                first_name = "John",
                last_name = "Doe",
                email = "john@example.com",
                company_name = "Acme Corp",
                segment = "Enterprise",
                addresses = new[]
                {
                    new { type = "Shipping", address_line_1 = "123 Main St", city = "Bangkok", postal_code = "10110" }
                }
            });
        }
        // Handle Customer Intent Extraction requests
        else if (systemPrompt.Contains("needs_customer_data") || request.SystemInstruction.Contains("determine if they need customer data"))
        {
            content = JsonSerializer.Serialize(new
            {
                needs_customer_data = true,
                customer_search_term = "Acme Corp",
                needs_history = false
            });
        }
        // Off-topic questions should redirect to manufacturing topics
        else if (lastMessage.Contains("weather") || lastMessage.Contains("sports") ||
                 lastMessage.Contains("politics") || lastMessage.Contains("movie") ||
                 lastMessage.Contains("joke") || lastMessage.Contains("fun"))
        {
            content = "I apologize, but I'm specifically designed to assist with manufacturing-related inquiries. " +
                     "I can help you with questions about our manufacturing processes, production capabilities, " +
                     "quality standards, and related topics. How can I assist you with your manufacturing needs today?";
        }
        // Competitor questions should politely redirect to our services
        else if (lastMessage.Contains("competitor") || lastMessage.Contains("CompetitorCorp") ||
                 lastMessage.Contains("other compan"))
        {
            content = "I focus on our manufacturing services and capabilities. " +
                     "We'd be happy to discuss our competitive advantages, quality standards, and how we can meet your specific requirements. " +
                     "Would you like to learn more about our services?";
        }
        // Chinese language should default to English response
        else if (lastMessage.Contains("你好") || lastMessage.Contains("中文"))
        {
            content = "I currently support English and Thai languages. How may I help you with your manufacturing inquiry?";
        }
        // Manufacturing-related topics
        else if (lastMessage.Contains("manufacturing") || lastMessage.Contains("production") ||
                 lastMessage.Contains("quality") || lastMessage.Contains("process") ||
                 lastMessage.Contains("capacity") || lastMessage.Contains("quotation") ||
                 lastMessage.Contains("order") || lastMessage.Contains("material"))
        {
            content = "Thank you for your manufacturing inquiry. " +
                     "We specialize in high-quality production with state-of-the-art processes. " +
                     "Our team can provide detailed information about our capabilities and create a custom quotation for your needs.";
        }
        // Default response
        else
        {
            content = "This is a mock response from Gemini API.";
        }

        return new GeminiResponse
        {
            Success = true,
            Content = content,
            TokenUsage = new GeminiTokenUsage { TotalTokens = 100 }
        };
    }
}

/// <summary>
/// Authorization handler that succeeds all requirements for integration tests.
/// </summary>
internal class AllowAllAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements.ToList())
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock implementation of LINE client for integration tests.
/// Always returns true for signature verification.
/// </summary>
internal class MockLineClient : ILineClient
{
    public Task SendTextMessageAsync(string replyToken, string messageText, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendFlexMessageAsync(string replyToken, string flexMessageJson, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public bool VerifySignature(string signature, string requestBody)
    {
        // Always return true for test webhooks
        return true;
    }
}

/// <summary>
/// Mock implementation of Meta client for integration tests.
/// Always returns true for signature verification.
/// </summary>
internal class MockMetaClient : IMetaClient
{
    public Task SendTextMessageAsync(string recipientId, string messageText, string platform, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SendGenericTemplateAsync(string recipientId, string templatePayload, string platform, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public bool VerifySignature(string signature, string requestBody)
    {
        // Always return true for test webhooks
        return true;
    }
}

/// <summary>
/// Mock implementation of Web Search Service for integration tests.
/// Returns mock search results to avoid external API calls.
/// </summary>
internal class MockWebSearchService : IWebSearchService
{
    public Task<List<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        // Return mock search results to avoid external API calls
        var results = new List<SearchResult>
        {
            new SearchResult
            {
                Title = "ISO 9001 - Quality management systems",
                Url = "https://www.iso.org/standard/62085.html",
                Snippet = "ISO 9001:2015 specifies requirements for a quality management system when an organization needs to demonstrate its ability to consistently provide products and services...",
                Domain = "iso.org"
            },
            new SearchResult
            {
                Title = "ISO 9001 - Wikipedia",
                Url = "https://en.wikipedia.org/wiki/ISO_9001",
                Snippet = "ISO 9001 is a standard that sets out the requirements for a quality management system. It helps businesses and organizations to be more efficient and improve customer satisfaction.",
                Domain = "wikipedia.org"
            },
            new SearchResult
            {
                Title = "What is ISO 9001? Quality Management System",
                Url = "https://asq.org/quality-resources/iso-9001",
                Snippet = "ISO 9001 is defined as the international standard that specifies requirements for a quality management system (QMS). Organizations use the standard to demonstrate...",
                Domain = "asq.org"
            }
        };

        return Task.FromResult(results);
    }
}

/// <summary>
/// Mock implementation of Operation Execution Service for integration tests.
/// Returns mock CRM operation results to avoid external service dependencies.
/// </summary>
internal class MockOperationExecutionService : IOperationExecutionService
{
    public Task<OperationResult> ExecuteOperationAsync(string operationType, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        switch (operationType)
        {
            case "QuotationQuery":
                var quotationId = parameters.ContainsKey("quotationId") ? parameters["quotationId"].ToString() : "Q-2025-1234";
                return Task.FromResult(new OperationResult
                {
                    Success = true,
                    Data = new
                    {
                        QuotationId = quotationId,
                        CustomerName = "ABC Manufacturing Ltd.",
                        Status = "Pending",
                        TotalAmount = 15000.00m,
                        ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
                        Items = new[]
                        {
                            new { PartNumber = "CNC-001", Description = "CNC Machined Part", Quantity = 100, UnitPrice = 150.00m }
                        }
                    },
                    FormattedMessage = $"Quotation {quotationId} for ABC Manufacturing Ltd. - Status: Pending, Total: $15,000.00. Valid until {DateTimeOffset.UtcNow.AddDays(30):yyyy-MM-dd}.",
                    SuggestedActions = new List<OperationAction>
                    {
                        new OperationAction
                        {
                            Label = "Send Reminder",
                            ActionType = "SendReminder",
                            Parameters = new Dictionary<string, object> { { "quotationId", quotationId! } }
                        },
                        new OperationAction
                        {
                            Label = "View Details",
                            ActionType = "ViewDetails",
                            Parameters = new Dictionary<string, object> { { "quotationId", quotationId! } }
                        }
                    }
                });

            case "SendReminder":
                var reminderQuotationId = parameters.ContainsKey("quotationId") ? parameters["quotationId"].ToString() : "Q-2025-1234";
                return Task.FromResult(new OperationResult
                {
                    Success = true,
                    FormattedMessage = $"Reminder sent successfully for quotation {reminderQuotationId} to ABC Manufacturing Ltd.",
                    SuggestedActions = new List<OperationAction>()
                });

            case "OrderStatusQuery":
                var orderId = parameters.ContainsKey("orderId") ? parameters["orderId"].ToString() : "O-2025-500";
                return Task.FromResult(new OperationResult
                {
                    Success = true,
                    Data = new
                    {
                        OrderId = orderId,
                        CustomerName = "XYZ Industries Inc.",
                        Status = "In Production",
                        EstimatedCompletion = DateTimeOffset.UtcNow.AddDays(7)
                    },
                    FormattedMessage = $"Order {orderId} for XYZ Industries Inc. - Status: In Production. Estimated completion: {DateTimeOffset.UtcNow.AddDays(7):yyyy-MM-dd}.",
                    SuggestedActions = new List<OperationAction>
                    {
                        new OperationAction
                        {
                            Label = "Update Status",
                            ActionType = "UpdateStatus",
                            Parameters = new Dictionary<string, object> { { "orderId", orderId! } }
                        }
                    }
                });

            case "CRMUpdate":
                return Task.FromResult(new OperationResult
                {
                    Success = true,
                    FormattedMessage = "CRM record updated successfully.",
                    SuggestedActions = new List<OperationAction>()
                });

            default:
                return Task.FromResult(new OperationResult
                {
                    Success = false,
                    ErrorMessage = $"Unknown operation type: {operationType}",
                    SuggestedActions = new List<OperationAction>()
                });
        }
    }
}
