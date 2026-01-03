# Implementation Plan: System Instruction Categorization and Dynamic Injection

**Branch**: `002-system-instruction-categorization` | **Date**: 2026-01-03 | **Spec**: [specs/002-system-instruction-categorization/spec.md]
**Input**: Feature specification from `/specs/002-system-instruction-categorization/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Enhance the SystemInstruction feature to support categorization into 'Core' (persona/safety) and 'Topic' (expert knowledge). User intent will be classified using a specialized Gemini model to dynamically inject relevant instructions and Knowledge Base facts into the prompt, ensuring both persona consistency and domain expertise while optimizing token usage.

## Technical Context

### Existing Components
- `SystemInstruction` Entity: Currently a single set of rules and persona.
- `SendMessageCommandHandler`: Orchestrates the message flow, currently fetches a single active instruction.
- `ISystemInstructionService`: Provides access to instructions, currently only `GetActiveInstructionAsync`.
- `BusinessConstraintValidator`: Validates if a message is on-topic based on current instructions.
- `IGeminiClient`: Interface for interacting with Gemini API.

### New Requirements
- **Categorized Instructions**: Support for "Core" (persona/safety) and "Topic" (domain-specific knowledge) instructions.
- **Intent Classification**: Using `gemini-2.5-flash-lite` to detect user intent and map it to `TopicKey`.
- **Knowledge Base**: A new table for granular fact retrieval (e.g., pricing) linked by `TopicKey`.
- **Instruction Merging**: Logic to combine Core and Topic instructions with specific precedence rules.
- **Caching Strategy**: Redis-based caching for merged instruction sets to minimize database lookups and redundant classification.

### Tech Stack
- .NET 10.0 (C# 13)
- EF Core 10 (PostgreSQL 18)
- StackExchange.Redis 2.x
- Google Gemini API (2.0/2.5)
- MassTransit (RabbitMQ) for events

### Unknowns (NEEDS CLARIFICATION)
- Should intent classification results be cached per-session or per-message?
- How many topics can be active simultaneously in a single prompt?
- What is the specific token limit threshold for dynamic injection?
- Does the Knowledge Base require vector search in the future, or is SQL/Indexed lookup sufficient for now?


## Constitution Check

| Rule | Status | Notes |
|------|--------|-------|
| No AutoMapper | ✅ | Explicit manual mapping will be used for DTOs. |
| No FluentValidation | ✅ | DataAnnotations and manual validation logic will be used. |
| No FluentAssertions | ✅ | Standard xUnit `Assert` will be used in tests. |
| Scalar for API Docs | ✅ | `MapScalarApiReference` is already in use. |
| Testcontainers for Integration Tests | ✅ | Real PostgreSQL and Redis will be used for tests. |
| Flat Project Structure | ✅ | Already followed (Api, Data, Application, etc. at root). |
| Zero Secrets in Code | ✅ | All config via environment variables. |
| XML Documentation | ✅ | Public methods will have documentation. |
| IAM Integration | ✅ | Will ensure new endpoints are secured via IAM permissions. |
| Docker Best Practices | ✅ | Dockerfile remains in Api project, using `app` user. |


## Project Structure

### Documentation (this feature)

```text
specs/002-system-instruction-categorization/
├── plan.md              # This file
├── research.md          # Strategy and decisions
├── data-model.md        # Entity definitions
├── quickstart.md        # Test guide
├── contracts/           # API specs
└── tasks.md             # Actionable task list
```

### Source Code

Following the established microservice pattern:
- **Maliev.ChatbotService.Api**: Controllers and DTOs
- **Maliev.ChatbotService.Application**: Command handlers and interfaces
- **Maliev.ChatbotService.Domain**: Entities and enums
- **Maliev.ChatbotService.Infrastructure**: Implementations and Data access
- **Maliev.ChatbotService.Tests**: Integration tests

**Structure Decision**: Flat project structure at repository root as per Constitution XV.

## Gates



- [ ] **Technical Alignment**: Does the plan solve the "Expert Persona" requirement while maintaining token efficiency?

- [ ] **Constitution Alignment**: Are all banned libraries avoided and mandatory practices followed?

- [ ] **Observability**: Are intent classification results and context sources logged in `MetadataJson`?

- [ ] **Performance**: Does the Redis caching strategy address potential latency from intent classification?


