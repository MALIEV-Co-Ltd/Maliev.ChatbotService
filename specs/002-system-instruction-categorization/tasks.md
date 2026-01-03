# Tasks: System Instruction Categorization and Dynamic Injection

## Phase 1: Setup
- [X] T001 Initialize feature branch `002-system-instruction-categorization`
- [X] T002 Configure `IntentClassification:ModelName` in `Maliev.ChatbotService.Api/appsettings.Development.json`

## Phase 2: Foundational
- [X] T003 [P] Create `SystemInstructionCategory` enum in `Maliev.ChatbotService.Domain/Enums/SystemInstructionCategory.cs`
- [X] T004 Update `SystemInstruction` entity with `Category`, `TopicKey`, and `Priority` in `Maliev.ChatbotService.Domain/Entities/SystemInstruction.cs`
- [X] T005 Create `KnowledgeBase` entity in `Maliev.ChatbotService.Domain/Entities/KnowledgeBase.cs`
- [X] T006 Update `SystemInstructionConfiguration` in `Maliev.ChatbotService.Infrastructure/Data/Configurations/SystemInstructionConfiguration.cs`
- [X] T007 Create `KnowledgeBaseConfiguration` in `Maliev.ChatbotService.Infrastructure/Data/Configurations/KnowledgeBaseConfiguration.cs`
- [X] T008 Update `ChatbotDbContext` with `KnowledgeBase` DbSet in `Maliev.ChatbotService.Infrastructure/Data/ChatbotDbContext.cs`
- [X] T009 Create EF Core migration `AddCategorizedInstructions` and update database

## Phase 3: [US1] Admin Configures Categorized Instructions
- [X] T010 [P] [US1] Update `SystemInstructionDto` with new properties in `Maliev.ChatbotService.Api/Models/Responses/SystemInstructionDto.cs`
- [X] T011 [P] [US1] Update `CreateSystemInstructionRequest` and `UpdateSystemInstructionRequest` in `Maliev.ChatbotService.Api/Models/Requests/`
- [X] T012 [US1] Define integration tests for categorized instruction management in `Maliev.ChatbotService.Tests/Integration/AdminApiTests.cs`
- [X] T013 [US1] Update `ISystemInstructionRepository` to support category and topic filtering in `Maliev.ChatbotService.Application/Interfaces/ISystemInstructionRepository.cs`
- [X] T014 [US1] Implement repository updates in `Maliev.ChatbotService.Infrastructure/Repositories/SystemInstructionRepository.cs`
- [X] T015 [US1] Update `SystemInstructionsController` to handle categories and topic keys in `Maliev.ChatbotService.Api/Controllers/V1/SystemInstructionsController.cs`

## Phase 4: [US2] Intent-Based Dynamic Instruction Injection
- [X] T016 [P] [US2] Create `IIntentClassificationService` interface in `Maliev.ChatbotService.Application/Interfaces/IIntentClassificationService.cs`
- [X] T017 [US2] Define integration tests for dynamic instruction injection in `Maliev.ChatbotService.Tests/Integration/DynamicInjectionTests.cs`
- [X] T018 [US2] Implement `IntentClassificationService` using `gemini-2.5-flash-lite` in `Maliev.ChatbotService.Infrastructure/Services/IntentClassificationService.cs`
- [X] T019 [US2] Update `ISystemInstructionService` with `GetMergedInstructionsAsync` in `Maliev.ChatbotService.Application/Interfaces/ISystemInstructionService.cs`
- [X] T020 [US2] Implement instruction merging and precedence logic in `Maliev.ChatbotService.Infrastructure/Services/SystemInstructionService.cs`
- [X] T021 [US2] Implement Redis caching for merged instruction sets in `Maliev.ChatbotService.Infrastructure/Services/SystemInstructionService.cs`
- [X] T022 [US2] Refactor `SendMessageCommandHandler` to include intent detection and dynamic instruction injection in `Maliev.ChatbotService.Application/Handlers/SendMessageCommandHandler.cs`
- [X] T023 [US2] Update `SendMessageCommandHandler` to store intent and source details in `MetadataJson`

## Phase 5: [US3] Knowledge Base Fact Retrieval
- [X] T024 [P] [US3] Create `IKnowledgeBaseRepository` in `Maliev.ChatbotService.Application/Interfaces/IKnowledgeBaseRepository.cs`
- [X] T025 [US3] Define integration tests for knowledge base fact injection in `Maliev.ChatbotService.Tests/Integration/KnowledgeBaseTests.cs`
- [X] T026 [US3] Implement `KnowledgeBaseRepository` in `Maliev.ChatbotService.Infrastructure/Repositories/KnowledgeBaseRepository.cs`
- [X] T027 [P] [US3] Create `KnowledgeBaseDto`, `CreateKnowledgeBaseRequest`, and `UpdateKnowledgeBaseRequest` in `Maliev.ChatbotService.Api/Models/`
- [X] T028 [US3] Create `KnowledgeBaseController` for admin management in `Maliev.ChatbotService.Api/Controllers/V1/KnowledgeBaseController.cs`
- [X] T029 [US3] Implement fact retrieval logic in `SendMessageCommandHandler` based on detected `TopicKey` in `Maliev.ChatbotService.Application/Handlers/SendMessageCommandHandler.cs`

## Phase 6: Polish & Cross-cutting Concerns

- [X] T030 Ensure 100% of LLM requests include the 'Core' persona definition

- [X] T031 Implement token limit management (safe threshold: 8k tokens) and priority-based truncation in `SystemInstructionService.cs`

- [X] T032 Implement business metrics for intent classification (success rate, confidence distribution) and context injection frequency

- [X] T033 Implement analytics for cache hit rates of merged instruction sets

- [X] T034 Final manual verification of success criteria (SC-001 to SC-004)

- [X] T035 Update OpenAPI documentation with Knowledge Base endpoints in `Maliev.ChatbotService.Api/Program.cs`
