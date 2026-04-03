# Maliev.ChatbotService — Agent Coding Guide

> This is an independent git repo within the `B:\maliev` workspace. All commands run from this directory.

---

## Build, Test & Lint Commands

All commands run from `B:\maliev\Maliev.ChatbotService`.

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.ChatbotService.slnx

# Run all tests
dotnet test Maliev.ChatbotService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~Maliev.ChatbotService.Tests.Domain.Entities.ConversationSessionTests.ShouldExpireAfter24Hours"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~ConversationSessionTests"

# Run with code coverage
dotnet test Maliev.ChatbotService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.ChatbotService.slnx

# Run API
dotnet run --project Maliev.ChatbotService.Api

# EF Core migrations (Infrastructure project only)
dotnet ef migrations add <Name> --project Maliev.ChatbotService.Infrastructure --startup-project Maliev.ChatbotService.Infrastructure

# EF Core database update
dotnet ef database update --project Maliev.ChatbotService.Infrastructure --startup-project Maliev.ChatbotService.Infrastructure
```

---

## Code Style & Conventions

### Workspace Structure
```
Maliev.ChatbotService/
├── Maliev.ChatbotService.Api/           # Controllers, Consumers, Middleware
├── Maliev.ChatbotService.Application/   # Use cases, DTOs, Interfaces, Handlers
├── Maliev.ChatbotService.Domain/        # Entities, value objects, domain interfaces
├── Maliev.ChatbotService.Infrastructure/ # EF Core DbContext, repositories, HTTP clients
├── Maliev.ChatbotService.Tests/         # Unit + Integration tests (xUnit)
├── Directory.Build.props                # Central package versioning
└── Maliev.ChatbotService.slnx          # Solution file (.slnx preferred over .sln)
```

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.ChatbotService.Domain.Entities;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `CreateSessionAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IConversationService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `chatbot.sessions.create`, `chatbot.messages.send`
  - Invalid: `chatbot.session.create` (singular), `chatbot.create` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("chatbot/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {SessionId}", sessionId)`
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned

### Domain Entities
- **IDs**: Use `Guid` for primary keys.
- **Dates**: Use `DateTimeOffset` instead of `DateTime`.
- **Collections**: Initialize collection properties (e.g., `public ICollection<Message> Messages { get; set; } = new List<Message>();`).
- **Navigation Properties**: Mark as nullable if optional.

---

## Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/chatbot/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

---

## Testing Rules

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

---

## Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("domain.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with `/chatbot`
- **Scalar docs**: Configured at `/chatbot/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead

---

## Service-Specific: LLM Integration

- **Provider**: Google Gemini
- **Primary Model**: Gemini 2.5 Flash
- **Intent Model**: Gemini 2.5 Flash Lite
- **Error Handling**: Returns fallback response messages when API fails or times out

### Responsibilities
- **Chatbot**: AI-powered customer support chatbot for general inquiries
- **Payment Slip Validation**: Auto-prevalidates bank transfer slips uploaded by customers. Marks slips as validated, but **ALL slips still require manual human admin approval** before production can proceed.

---

## Workflows

### Adding a New Feature
1. Define Entity in `Domain`.
2. Create Repository Interface in `Domain/Interfaces`.
3. Implement Repository in `Infrastructure`.
4. Create Service/Handler in `Application`.
5. Create Controller/Endpoint in `Api`.
6. Add Integration Tests in `Tests`.

### Modifying Database
1. Modify Entity in `Domain`.
2. Create Migration (`dotnet ef migrations add ...`).
3. Update `DbContext` in `Infrastructure` if necessary.

---

## Git Rules

- This is an independent git repo. All git commands run from `B:\maliev\Maliev.ChatbotService`
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked
