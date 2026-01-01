# Implementation Plan: Unified Chatbot Service

**Branch**: `001-unified-chatbot-service` | **Date**: 2025-12-30 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/001-unified-chatbot-service/spec.md`

## Summary
The Unified Chatbot Service is a central conversational AI layer for the Maliev ecosystem. It integrates with Gemini API (gemini-3-flash, configurable via appsettings.json) to provide multimodal (text, image, audio, PDF) handling across multiple channels (Website, LINE, Facebook, Instagram, WhatsApp). It features persistent user memory, session management, internal tool execution (CRM queries), and strict business boundary enforcement.

## Technical Context

**Language/Version**: .NET 10.0 (C# 13)
**Primary Dependencies**: 
- `Google.Cloud.AIPlatform.V1` (Vertex AI) OR `Mscc.GenerativeAI` / HttpClient (Gemini API)
- `Line.Messaging` (Community SDK)
- `WhatsappBusiness.CloudApi` OR HttpClient
- `Maliev.Aspire.ServiceDefaults`
- `Maliev.MessagingContracts`
- `MassTransit` (RabbitMQ)
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `StackExchange.Redis`
**Storage**: PostgreSQL 18 (Sessions, Memory, Instructions), Redis 7.x (Cache)
**Testing**: xUnit, Testcontainers, NSubstitute
**Target Platform**: Kubernetes (Linux)
**Project Type**: Web API (Microservice)
**Performance Goals**: <3s response time (p90), 1000 concurrent sessions
**Constraints**: No AutoMapper, No FluentValidation, No FluentAssertions. 

## Constitution Check

*GATE: Passed*
- **Architecture**: 5-Layer Clean Architecture selected.
- **Libraries**: No banned libraries included.
- **Documentation**: XML docs required.
- **Secrets**: Env vars only.

## Project Structure

### Documentation (this feature)

```text
specs/001-unified-chatbot-service/
├── plan.md              # This file
├── research.md          # Technical decisions
├── data-model.md        # DB Schema
├── quickstart.md        # Local run guide
├── contracts/           # OpenAPI spec
└── tasks.md             # Pending
```

### Source Code (repository root)

```text
Maliev.ChatbotService/
├── Maliev.ChatbotService.Api/           # Controllers, Webhooks
├── Maliev.ChatbotService.Application/   # CQRS, LLM Orchestration
├── Maliev.ChatbotService.Domain/        # Entities, Logic
├── Maliev.ChatbotService.Infrastructure/# Gemini Client, Repositories
└── Maliev.ChatbotService.Tests/         # Integration Tests
```

**Structure Decision**: Standard Maliev 5-Layer Architecture.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 5-Layer Arch | Complex domain with external AI integration, state machine, and multi-channel adapters. | 3-Layer CRUD is insufficient for managing conversation state + LLM context + channel adaptation logic cleanly. |

## Database Convention

- **DbContext**: `ChatbotDbContext`
- **Database**: `chatbot_app_db`
- **Connection String Key**: `ConnectionStrings:ChatbotDbContext`

## IAM Permissions (GCP-Style)

| Permission | Description |
|------------|-------------|
| `chatbot.sessions.create` | Initiate a new conversation session |
| `chatbot.sessions.read` | View session details and history |
| `chatbot.messages.send` | Send messages to the chatbot |
| `chatbot.messages.read` | View message history |
| `chatbot.preferences.read` | View stored user preferences |
| `chatbot.preferences.write` | Modify user preferences |
| `chatbot.preferences.delete` | Delete user preferences |
| `chatbot.webhooks.receive` | Receive webhook events from external channels |
| `chatbot.instructions.read` | View system instructions (admin) |
| `chatbot.instructions.write` | Modify system instructions (admin) |

## Messaging Contracts (RabbitMQ Events)

| Event | Published When |
|-------|----------------|
| `ChatbotSessionCreatedEvent` | New session initiated |
| `ChatbotSessionClosedEvent` | Session expired or closed |
| `ChatbotMessageReceivedEvent` | User message received |
| `ChatbotRateLimitExceededEvent` | User exceeded 100 msg/hr limit |