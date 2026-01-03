# Maliev.ChatbotService Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-01-03

## Active Technologies

- .NET 10.0 + ASP.NET Core
- Entity Framework Core 10 (PostgreSQL 18)
- Redis 7.x (StackExchange.Redis)
- Google Gemini API (2.0/2.5)
- MassTransit (RabbitMQ)

## Project Structure

```text
Maliev.ChatbotService.Api/
Maliev.ChatbotService.Application/
Maliev.ChatbotService.Domain/
Maliev.ChatbotService.Infrastructure/
Maliev.ChatbotService.Tests/
```

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project Maliev.ChatbotService.Api`
- Migrations: `dotnet ef migrations add <Name> --project Maliev.ChatbotService.Infrastructure`

## Code Style

.NET 10.0: Follow standard conventions. NO AutoMapper, NO FluentValidation, NO FluentAssertions.

## Recent Changes

- 001-unified-chatbot-service: Initial implementation of the unified chatbot service with Gemini integration, web search, and session management.
- 002-system-instruction-categorization: Added support for categorized system instructions (Core vs Topic), intent classification for dynamic injection, and Knowledge Base fact retrieval.

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
