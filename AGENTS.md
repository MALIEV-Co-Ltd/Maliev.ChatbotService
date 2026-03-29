# Agentic Coding Instructions for Maliev.ChatbotService

This document contains instructions for AI agents operating in this repository.

## 1. Environment & Build

- **Framework**: .NET 10.0 (C# 13)
- **Database**: PostgreSQL 18 (using Entity Framework Core 10)
- **Architecture**: Clean Architecture (Api, Application, Domain, Infrastructure, Tests)
- **TreatWarningsAsErrors**: ENABLED. Zero compilation warnings allowed.

### Commands

- **Build**: `dotnet build`
- **Test (All)**: `dotnet test`
- **Test (Single)**: `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"`
  - *Example*: `dotnet test --filter "FullyQualifiedName~Maliev.ChatbotService.Tests.Domain.Entities.ConversationSessionTests.ShouldExpireAfter24Hours"`
- **Run API**: `dotnet run --project Maliev.ChatbotService.Api`
- **Database Migrations**: `dotnet ef migrations add <MigrationName> --project Maliev.ChatbotService.Infrastructure --startup-project Maliev.ChatbotService.Infrastructure`
- **Database Update**: `dotnet ef database update --project Maliev.ChatbotService.Infrastructure --startup-project Maliev.ChatbotService.Infrastructure`

## 2. Code Style & Conventions

### General
- **Namespaces**: Use file-scoped namespaces (e.g., `namespace Maliev.ChatbotService.Domain.Entities;`).
- **Formatting**: Standard C# conventions (PascalCase for classes/methods, camelCase for local variables).
- **Nullability**: `Nullable` context is ENABLED. Handle nulls explicitly. Use `?` for optional references.
- **Documentation**: XML documentation `///` is **REQUIRED** for all public methods and properties.

### Domain Entities
- **IDs**: Use `Guid` for primary keys.
- **Dates**: Use `DateTimeOffset` instead of `DateTime`.
- **Collections**: Initialize collection properties (e.g., `public ICollection<Message> Messages { get; set; } = new List<Message>();`).
- **Navigation Properties**: Mark as nullable if optional.

### Architecture Rules (Strict)
- **No AutoMapper**: Perform manual mapping.
- **No FluentValidation**: Use Data Annotations (`[Required]`, `[EmailAddress]`).
- **No FluentAssertions**: Use standard xUnit `Assert`.
- **No In-Memory DB**: Use **Testcontainers** for integration tests.
- **No Secrets**: Configuration via environment variables only.

## 3. Testing Guidelines

- **Integration over Unit**: Prioritize integration tests using Testcontainers/PostgreSQL.
- **Naming**: `MethodName_StateUnderWhichTestIsRunning_ExpectedBehavior` (e.g., `CreateSession_WithValidData_ReturnsSessionId`).
- **Structure**: Arrange, Act, Assert comments are optional but encouraged for complex tests.

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

#### Key Rules
- Use `BaseIntegrationTestFactory<TProgram, TDbContext>` for integration tests (real Testcontainers, never InMemoryDatabase)
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Minimum 80% code coverage
- Use `[Fact]` for single cases, `[Theory]` for parameterized tests

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

## 4. Specific Workflows

### LLM Integration
- **Provider**: Google Gemini
- **Primary Model**: Gemini 2.5 Flash
- **Intent Model**: Gemini 2.5 Flash Lite
- **Error Handling**: Returns fallback response messages when API fails or times out

### Responsibilities
- **Chatbot**: AI-powered customer support chatbot for general inquiries
- **Payment Slip Validation**: Auto-prevalidates bank transfer slips uploaded by customers. Marks slips as validated, but **ALL slips still require manual human admin approval** before production can proceed.

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

## 5. Agent Behavior
- **Proactive Fixes**: If you see a warning, fix it.
- **Verification**: ALWAYS run `dotnet build` after changes.
- **Safety**: Do not commit secrets.


## Git & Version Control — Mandatory Rules

### 🚨 CRITICAL: Always Commit Code Changes (Non-Negotiable)
- **You MUST commit your changes to the local repository after completing any meaningful unit of work.**
- **Never accumulate uncommitted changes.** Do not wait until end of session or until something breaks.
- **Commit early and often** — if a change is meaningful (even a small fix or refactor), commit it.
- **You do NOT need to push to remote** — local commits are sufficient to protect against accidental loss.
- **If you are unsure whether to commit, commit anyway.** Extra commits are harmless; lost work is irreversible.
- This rule applies even if you are just "testing" or "exploring" — use git branches to isolate experimental work and commit those changes too.

### 🚨 CRITICAL: Never Use `git checkout` to Restore Broken Files
- **NEVER use `git checkout` to restore or recover files.** This operation discards uncommitted changes permanently and will result in data loss.
- **To undo/recover from broken files: first commit your current changes, then use `git revert` or `git reset --soft` to safely undo.**

## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
