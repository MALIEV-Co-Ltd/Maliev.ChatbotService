# Tasks: Unified Chatbot Service

**Input**: Design documents from `/specs/001-unified-chatbot-service/`
**Prerequisites**: plan.md (✓), spec.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓)

**Tests**: ✅ Following Constitution III - Tests MUST be written BEFORE implementation

**Organization**: Tasks follow Test-First Development - Phase 1 (Setup) → Phase 2 (Write ALL Tests) → Phase 3+ (Implement to pass tests)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

This project uses Maliev's 5-Layer Clean Architecture:
- **Api**: `Maliev.ChatbotService.Api/`
- **Application**: `Maliev.ChatbotService.Application/`
- **Domain**: `Maliev.ChatbotService.Domain/`
- **Infrastructure**: `Maliev.ChatbotService.Infrastructure/`
- **Tests**: `Maliev.ChatbotService.Tests/`

---

## Phase 1: Project Setup (Infrastructure Only - No Implementation)

**Purpose**: Initialize project structure and build system following Maliev constitution

- [X] T001 Create solution file Maliev.ChatbotService.sln at repository root
- [X] T002 [P] Create Maliev.ChatbotService.Api project with .NET 10.0
- [X] T003 [P] Create Maliev.ChatbotService.Application project with .NET 10.0
- [X] T004 [P] Create Maliev.ChatbotService.Domain project with .NET 10.0
- [X] T005 [P] Create Maliev.ChatbotService.Infrastructure project with .NET 10.0
- [X] T006 [P] Create Maliev.ChatbotService.Tests project with .NET 10.0, xUnit, and Testcontainers
- [X] T007 Configure project references (Api → Application, Application → Domain, Infrastructure → Application/Domain, Tests → all)
- [X] T008 [P] Add Maliev.Aspire.ServiceDefaults NuGet package to Api and Infrastructure projects (deferred to Phase 3 - requires implementation)
- [X] T009 [P] Add Maliev.MessagingContracts NuGet package to Application and Infrastructure projects (deferred to Phase 3 - requires implementation)
- [X] T010 [P] Configure TreatWarningsAsErrors=true and GenerateDocumentationFile=true in all .csproj files
- [X] T011 [P] Create nuget.config at repository root with GitHub Packages source and credential placeholders
- [X] T012 [P] Create .pre-commit-config.yaml with standard Maliev linting rules
- [X] T013 Create .gitignore with standard .NET patterns (bin/, obj/, *.user, .vs/, *.suo)
- [X] T014 Create .dockerignore with patterns: .git/, specs/, *.md (except README.md), tests/, bin/, obj/, .vs/, *.user
- [X] T015 Create Dockerfile in Maliev.ChatbotService.Api/ following Constitution X pattern with BuildKit secrets
- [X] T016 Create appsettings.json in Api project with Gemini model configuration and standard logging per Constitution V
- [X] T017 Create appsettings.Development.json in Api project with localhost connection strings
- [X] T018 [P] Create .github/workflows/ci-develop.yml for development environment CI/CD (already exists)
- [X] T019 [P] Create .github/workflows/ci-staging.yml for staging environment CI/CD (already exists)
- [X] T020 [P] Create .github/workflows/ci-main.yml for production environment CI/CD (already exists)
- [X] T021 Create .github/CODEOWNERS file with content: `* @MALIEV-Co-Ltd/core-developers`
- [X] T022 Verify NO docker-compose.yml present (Constitution XVI compliance)
- [X] T023 Verify NO additional .md files in repository root beyond README.md (Constitution IX compliance)
- [X] T024 Create TestcontainersIntegrationTestFactory base class in Maliev.ChatbotService.Tests/Infrastructure/TestcontainersIntegrationTestFactory.cs
- [X] T025 Configure Testcontainers for PostgreSQL 18, Redis 7.x, and RabbitMQ in test factory

**Checkpoint**: Project structure ready, constitution compliance verified ✅

---

## Phase 2: Test Authoring (TEST-FIRST: Write ALL Tests BEFORE Implementation)

**Purpose**: Author ALL tests for ALL features BEFORE writing any implementation code (Constitution III)

**⚠️ CRITICAL**: These tests MUST FAIL initially - this proves tests are valid. Implementation in later phases will make them pass.

### Foundation Tests

- [X] T026 [P] Write test for ChatbotDbContext: Verify all entities configured correctly in Maliev.ChatbotService.Tests/Integration/DatabaseTests.cs
- [X] T027 [P] Write test for database migrations: Verify InitialCreate migration applies successfully
- [X] T028 [P] Write test for UserProfile repository: CRUD operations
- [X] T029 [P] Write test for ConversationSession repository: CRUD and session expiry queries
- [X] T030 [P] Write test for Message repository: CRUD and session message retrieval
- [X] T031 [P] Write test for UserMemory repository: CRUD and user preference queries
- [X] T032 [P] Write test for SystemInstruction repository: CRUD and active instruction retrieval
- [X] T033 [P] Write test for IdentityLink repository: CRUD and platform linking queries
- [X] T034 [P] Write test for OperationLog repository: CRUD and operation history queries
- [X] T035 [P] Write test for SystemInstructionService: Redis caching with PostgreSQL fallback
- [X] T036 [P] Write test for RateLimitService: Sliding window rate limiting with Redis
- [X] T037 [P] Write test for InputValidationService: Injection attack prevention for SQL, XSS, command injection
- [X] T038 [P] Write test for GeminiClient: API call with retry logic and timeout handling

### User Story 1 Tests (Website Manufacturing Inquiry)

- [X] T039 [US1] Write contract test: POST /v1/sessions/initiate returns session with English default language
- [X] T040 [US1] Write contract test: POST /v1/sessions/initiate with Thai message returns session with Thai language
- [X] T041 [US1] Write contract test: POST /v1/messages with manufacturing inquiry returns structured response with buttons
- [X] T042 [US1] Write integration test: Complete conversation flow from session initiation to structured manufacturing response
- [X] T043 [US1] Write integration test: Language detection switches to Thai when Thai message received
- [X] T044 [US1] Write integration test: Business constraint enforcement rejects off-topic weather question
- [X] T045 [US1] Write integration test: Unsupported language (Chinese) receives English default response

### User Story 2 Tests (Cross-Platform Continuity)

- [X] T046 [US2] Write contract test: POST /v1/users/link initiates identity linking with webhook confirmation
- [X] T047 [US2] Write integration test: Session initiated on website, continued on LINE with same authenticated user retrieves context
- [X] T048 [US2] Write integration test: Previous session summary retrieved and included in new session context
- [X] T049 [US2] Write integration test: Session expires after 24 hours and conversation summary is generated
- [X] T050 [US2] Write integration test: Language consistency maintained across platform switch

### User Story 3 Tests (Multimodal Input)

- [X] T051 [US3] Write contract test: POST /v1/messages with image attachment (under 10MB) processes successfully
- [X] T052 [US3] Write contract test: POST /v1/messages with PDF attachment (under 20MB) processes successfully
- [X] T053 [US3] Write contract test: POST /v1/messages with video attachment (under 50MB) processes successfully
- [X] T054 [US3] Write contract test: POST /v1/messages with oversized image (>10MB) returns 400 with clear error
- [X] T055 [US3] Write integration test: Image analysis provides manufacturing process identification
- [X] T056 [US3] Write integration test: PDF technical drawing extracts dimensions and materials

### User Story 4 Tests (Business Context Enforcement)

- [X] T057 [US4] Write integration test: Weather question receives professional rejection with manufacturing redirection
- [X] T058 [US4] Write integration test: Competitor services question receives polite decline
- [X] T059 [US4] Write integration test: Repeated off-topic requests maintain same professional boundary

### User Story 5 Tests (Persistent User Preferences)

- [X] T060 [US5] Write contract test: GET /v1/users/me/preferences returns paginated preference list
- [X] T061 [US5] Write contract test: DELETE /v1/users/me/data with scope=preferences requires confirmation
- [X] T062 [US5] Write integration test: Stated preference "stainless steel 304" is stored with high confidence
- [X] T063 [US5] Write integration test: New session retrieves stored preferences and offers to reuse
- [X] T064 [US5] Write integration test: User deletion request with confirmation deletes selected scope

### User Story 6 Tests (Internal CRM Agent)

- [X] T065 [US6] Write contract test: POST /v1/messages from internal agent with IAM authentication queries quotation status
- [X] T066 [US6] Write integration test: Authenticated sales agent queries quotation Q-2025-1234 and receives structured data with actions
- [X] T067 [US6] Write integration test: Unauthenticated user attempting CRM query receives 403 Forbidden
- [X] T068 [US6] Write integration test: Internal agent executes "Send Reminder" operation successfully

### User Story 7 Tests (Graceful Degradation)

- [X] T069 [US7] Write integration test: Gemini API timeout (10s) triggers fallback response
- [X] T070 [US7] Write integration test: Gemini API unavailable returns predefined fallback with contact info
- [X] T071 [US7] Write integration test: Gemini response fails schema validation, retries with enhanced prompt, then falls back
- [X] T072 [US7] Write integration test: Redis unavailable triggers PostgreSQL fallback with acceptable performance degradation
- [X] T073 [US7] Write integration test: Redis recovery detected and caching resumes automatically

### User Story 8 Tests (Controlled Web Search)

- [X] T074 [US8] Write integration test: Technical spec query "ASTM A36 steel properties" triggers Google Custom Search when permitted
- [X] T075 [US8] Write integration test: Search domains are logged to SearchDomainLog table
- [X] T076 [US8] Write integration test: Web search disabled in system instructions prevents search execution
- [X] T077 [US8] Write integration test: Competitor pricing query refused despite search capability
- [X] T078 [US8] Write integration test: Web search query timeout (30s) triggers fallback response

### Webhook Integration Tests

- [X] T079 Write contract test: POST /v1/webhooks/line with valid signature processes LINE message
- [X] T080 Write contract test: POST /v1/webhooks/facebook with valid signature processes Facebook message
- [X] T081 Write contract test: POST /v1/webhooks/whatsapp with valid signature processes WhatsApp message
- [X] T082 Write integration test: LINE Flex Message formatting for structured response
- [X] T083 Write integration test: Facebook Generic Template formatting for structured response
- [X] T084 Write integration test: Website UI component formatting for structured response

### Admin API Tests

- [X] T085 Write contract test: GET /v1/admin/instructions with authentication returns system instructions list
- [X] T086 Write contract test: POST /v1/admin/instructions with authentication creates new instruction
- [X] T087 Write contract test: PUT /v1/admin/instructions/{id} with authentication updates instruction
- [X] T088 Write contract test: DELETE /v1/admin/instructions/{id} with authentication deactivates instruction
- [X] T089 Write integration test: Admin API requires chatbot.instructions.read permission
- [X] T090 Write integration test: Admin API requires chatbot.instructions.write permission for mutations

### Business Metrics Tests (Constitution XII)

- [X] T091 Write test: GET /chatbot/metrics exposes conversation_volume counter
- [X] T092 Write test: GET /chatbot/metrics exposes response_latency_p90 histogram
- [X] T093 Write test: GET /chatbot/metrics exposes gemini_api_success_rate gauge
- [X] T094 Write test: GET /chatbot/metrics exposes active_sessions_count gauge
- [X] T095 Write test: GET /chatbot/metrics exposes user_satisfaction_score gauge (if available)
- [X] T096 Write test: Metrics include required tags (service_name, version, region, environment)

### Messaging Tests

- [X] T097 Write test: SessionCreated event published to RabbitMQ when session initiated
- [X] T098 Write test: SessionClosed event published to RabbitMQ when session expires
- [X] T099 Write test: MessageReceived event published to RabbitMQ when user message processed
- [X] T100 Write test: RateLimitExceeded event published to RabbitMQ when 100 msg/hr exceeded

**Checkpoint**: ALL tests written and FAILING (Red phase of Red-Green-Refactor). Implementation can now begin. ✅

---

## Phase 3: Foundation Implementation (Make Foundation Tests Pass)

**Purpose**: Implement core infrastructure to make foundation tests pass (Green phase)

### Domain Layer

- [X] T101 [P] Create Channel enum (Website, Line, Facebook, Instagram, WhatsApp, Intranet) in Maliev.ChatbotService.Domain/Enums/Channel.cs
- [X] T102 [P] Create Language enum (English, Thai) in Maliev.ChatbotService.Domain/Enums/Language.cs
- [X] T103 [P] Create SessionStatus enum (Active, Closed) in Maliev.ChatbotService.Domain/Enums/SessionStatus.cs
- [X] T104 [P] Create UserRole enum (Customer, InternalAgent) in Maliev.ChatbotService.Domain/Enums/UserRole.cs
- [X] T105 [P] Create MessageRole enum (User, Assistant, System) in Maliev.ChatbotService.Domain/Enums/MessageRole.cs
- [X] T106 [P] Create ContentType enum (Text, Image, Audio, PDF, Video) in Maliev.ChatbotService.Domain/Enums/ContentType.cs
- [X] T107 [P] Create WebhookConfirmationStatus enum (Pending, Confirmed, Failed) in Maliev.ChatbotService.Domain/Enums/WebhookConfirmationStatus.cs
- [X] T108 [P] Create UserProfile entity in Maliev.ChatbotService.Domain/Entities/UserProfile.cs
- [X] T109 [P] Create ConversationSession entity in Maliev.ChatbotService.Domain/Entities/ConversationSession.cs
- [X] T110 [P] Create Message entity in Maliev.ChatbotService.Domain/Entities/Message.cs
- [X] T111 [P] Create ConversationSummary entity in Maliev.ChatbotService.Domain/Entities/ConversationSummary.cs
- [X] T112 [P] Create UserMemory entity in Maliev.ChatbotService.Domain/Entities/UserMemory.cs
- [X] T113 [P] Create SystemInstruction entity in Maliev.ChatbotService.Domain/Entities/SystemInstruction.cs
- [X] T114 [P] Create IdentityLink entity in Maliev.ChatbotService.Domain/Entities/IdentityLink.cs
- [X] T115 [P] Create OperationLog entity in Maliev.ChatbotService.Domain/Entities/OperationLog.cs
- [X] T116 [P] Create SearchDomainLog entity in Maliev.ChatbotService.Domain/Entities/SearchDomainLog.cs

### Infrastructure Layer - Database

- [X] T117 Create ChatbotDbContext in Maliev.ChatbotService.Infrastructure/Data/ChatbotDbContext.cs with all entity configurations
- [X] T118 [P] Create UserProfile entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/UserProfileConfiguration.cs
- [X] T119 [P] Create ConversationSession entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/ConversationSessionConfiguration.cs
- [X] T120 [P] Create Message entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/MessageConfiguration.cs
- [X] T121 [P] Create ConversationSummary entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/ConversationSummaryConfiguration.cs
- [X] T122 [P] Create UserMemory entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/UserMemoryConfiguration.cs
- [X] T123 [P] Create SystemInstruction entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/SystemInstructionConfiguration.cs
- [X] T124 [P] Create IdentityLink entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/IdentityLinkConfiguration.cs
- [X] T125 [P] Create OperationLog entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/OperationLogConfiguration.cs
- [X] T126 [P] Create SearchDomainLog entity configuration in Maliev.ChatbotService.Infrastructure/Data/Configurations/SearchDomainLogConfiguration.cs
- [X] T127 Create initial EF Core migration using: dotnet ef migrations add InitialCreate
- [X] T128 [P] Create IUserProfileRepository interface in Maliev.ChatbotService.Application/Interfaces/IUserProfileRepository.cs
- [X] T129 [P] Create IConversationSessionRepository interface in Maliev.ChatbotService.Application/Interfaces/IConversationSessionRepository.cs
- [X] T130 [P] Create IMessageRepository interface in Maliev.ChatbotService.Application/Interfaces/IMessageRepository.cs
- [X] T131 [P] Create IUserMemoryRepository interface in Maliev.ChatbotService.Application/Interfaces/IUserMemoryRepository.cs
- [X] T132 [P] Create ISystemInstructionRepository interface in Maliev.ChatbotService.Application/Interfaces/ISystemInstructionRepository.cs
- [X] T133 [P] Create IIdentityLinkRepository interface in Maliev.ChatbotService.Application/Interfaces/IIdentityLinkRepository.cs
- [X] T134 [P] Create IOperationLogRepository interface in Maliev.ChatbotService.Application/Interfaces/IOperationLogRepository.cs
- [X] T135 [P] Create ISearchDomainLogRepository interface in Maliev.ChatbotService.Application/Interfaces/ISearchDomainLogRepository.cs
- [X] T136 [P] Implement UserProfileRepository in Maliev.ChatbotService.Infrastructure/Repositories/UserProfileRepository.cs
- [X] T137 [P] Implement ConversationSessionRepository in Maliev.ChatbotService.Infrastructure/Repositories/ConversationSessionRepository.cs
- [X] T138 [P] Implement MessageRepository in Maliev.ChatbotService.Infrastructure/Repositories/MessageRepository.cs
- [X] T139 [P] Implement UserMemoryRepository in Maliev.ChatbotService.Infrastructure/Repositories/UserMemoryRepository.cs
- [X] T140 [P] Implement SystemInstructionRepository in Maliev.ChatbotService.Infrastructure/Repositories/SystemInstructionRepository.cs
- [X] T141 [P] Implement IdentityLinkRepository in Maliev.ChatbotService.Infrastructure/Repositories/IdentityLinkRepository.cs
- [X] T142 [P] Implement OperationLogRepository in Maliev.ChatbotService.Infrastructure/Repositories/OperationLogRepository.cs
- [X] T143 [P] Implement SearchDomainLogRepository in Maliev.ChatbotService.Infrastructure/Repositories/SearchDomainLogRepository.cs

### Infrastructure Layer - External Services

- [X] T144 [P] Create IGeminiClient interface in Maliev.ChatbotService.Application/Interfaces/IGeminiClient.cs
- [X] T145 Create GeminiRequest and GeminiResponse DTOs in Maliev.ChatbotService.Infrastructure/AI/Models/
- [X] T146 Implement typed HttpClient-based GeminiClient in Maliev.ChatbotService.Infrastructure/AI/GeminiClient.cs with standard resilience, retry logic, timeout handling (10s default, 30s for web search)
- [X] T147 [P] Create ISystemInstructionService interface in Maliev.ChatbotService.Application/Interfaces/ISystemInstructionService.cs
- [X] T148 Implement SystemInstructionService with Redis caching and PostgreSQL fallback in Maliev.ChatbotService.Infrastructure/Services/SystemInstructionService.cs
- [X] T149 [P] Create IRateLimitService interface in Maliev.ChatbotService.Application/Interfaces/IRateLimitService.cs
- [X] T150 Implement RateLimitService with Redis sliding window (100 msg/hr) in Maliev.ChatbotService.Infrastructure/Services/RateLimitService.cs
- [X] T151 [P] Create IInputValidationService interface in Maliev.ChatbotService.Application/Interfaces/IInputValidationService.cs
- [X] T152 Implement InputValidationService with SQL injection, XSS, command injection prevention in Maliev.ChatbotService.Infrastructure/Services/InputValidationService.cs
- [X] T153 [P] Create IResponseTimeoutService interface in Maliev.ChatbotService.Application/Interfaces/IResponseTimeoutService.cs
- [X] T154 Implement ResponseTimeoutService with configurable timeouts (10s default, 30s web search) in Maliev.ChatbotService.Infrastructure/Services/ResponseTimeoutService.cs
- [X] T155 [P] Create IWebSearchService interface in Maliev.ChatbotService.Application/Interfaces/IWebSearchService.cs
- [X] T156 Implement WebSearchService using Google Custom Search API with typed HttpClient in Maliev.ChatbotService.Infrastructure/Services/WebSearchService.cs
- [X] T157 [P] Create IIAMServiceClient interface in Maliev.ChatbotService.Application/Interfaces/IIAMServiceClient.cs
- [X] T158 Implement IAMServiceClient with typed HttpClient in Maliev.ChatbotService.Infrastructure/ExternalServices/IAMServiceClient.cs

### API Layer - Program.cs Configuration

- [X] T159 Configure Program.cs with builder.AddServiceDefaults()
- [X] T160 Configure Program.cs with builder.AddJwtAuthentication()
- [X] T161 Configure Program.cs with builder.AddPostgresDbContext<ChatbotDbContext>("ChatbotDbContext")
- [X] T162 Configure Program.cs with builder.AddRedisDistributedCache("Cache")
- [X] T163 Configure Program.cs with builder.AddMassTransitWithRabbitMq()
- [X] T164 Configure Program.cs with builder.AddDefaultApiVersioning()
- [X] T165 Configure Program.cs with builder.AddDefaultCors()
- [X] T166 Configure Program.cs with app.MigrateDatabaseAsync<ChatbotDbContext>() on startup
- [X] T167 Configure Program.cs with app.MapDefaultEndpoints() for /liveness, /readiness, /metrics
- [X] T168 Configure Program.cs with app.MapApiDocumentation("chatbot") for /scalar, /openapi
- [X] T169 Configure dependency injection for all repositories and services in Program.cs
- [X] T170 Create HealthController with liveness and readiness endpoints in Maliev.ChatbotService.Api/Controllers/HealthController.cs
- [X] T171 Create error handling middleware in Maliev.ChatbotService.Api/Middleware/ErrorHandlingMiddleware.cs
- [X] T172 Create correlation ID middleware in Maliev.ChatbotService.Api/Middleware/CorrelationIdMiddleware.cs

### Database Seed Data

- [X] T173 Create database seed data with default system instruction for manufacturing context with gemini-3-flash model configuration

**Checkpoint**: Foundation tests now PASS - infrastructure ready for user story implementation ✅

---

## Phase 4: User Story 1 Implementation (Website Manufacturing Inquiry)

**Purpose**: Implement US1 to make its tests pass

- [X] T174 [P] [US1] Create InitiateSessionRequest DTO in Maliev.ChatbotService.Api/Models/Requests/InitiateSessionRequest.cs with Data Annotations validation
- [X] T175 [P] [US1] Create SessionResponse DTO in Maliev.ChatbotService.Api/Models/Responses/SessionResponse.cs
- [X] T176 [P] [US1] Create SendMessageRequest DTO in Maliev.ChatbotService.Api/Models/Requests/SendMessageRequest.cs with Data Annotations validation
- [X] T177 [P] [US1] Create MessageResponse DTO in Maliev.ChatbotService.Api/Models/Responses/MessageResponse.cs
- [X] T178 [P] [US1] Create SuggestedAction DTO in Maliev.ChatbotService.Api/Models/Responses/SuggestedAction.cs
- [X] T179 [US1] Create InitiateSessionCommand in Maliev.ChatbotService.Application/Commands/InitiateSessionCommand.cs
- [X] T180 [US1] Create InitiateSessionCommandHandler in Maliev.ChatbotService.Application/Handlers/InitiateSessionCommandHandler.cs with session creation and language detection
- [X] T181 [US1] Create SendMessageCommand in Maliev.ChatbotService.Application/Commands/SendMessageCommand.cs
- [X] T182 [US1] Create SendMessageCommandHandler in Maliev.ChatbotService.Application/Handlers/SendMessageCommandHandler.cs with rate limiting, queue management, Gemini integration, input validation
- [X] T183 [US1] Create LanguageDetectionService in Maliev.ChatbotService.Infrastructure/Services/LanguageDetectionService.cs to detect English vs Thai
- [X] T184 [US1] Create ResponseFormatterService in Maliev.ChatbotService.Infrastructure/Services/ResponseFormatterService.cs for structured responses with buttons
- [X] T185 [US1] Create SessionsController in Maliev.ChatbotService.Api/Controllers/V1/SessionsController.cs with POST /v1/sessions/initiate
- [X] T186 [US1] Create MessagesController in Maliev.ChatbotService.Api/Controllers/V1/MessagesController.cs with POST /v1/messages
- [X] T187 [US1] Implement business constraint enforcement in SendMessageCommandHandler to reject off-topic requests
- [X] T188 [US1] Add XML documentation to all public methods in US1 controllers, handlers, and services

**Checkpoint**: User Story 1 tests now PASS - website conversation works

---

## Phase 5: User Story 2 Implementation (Cross-Platform Continuity)

**Purpose**: Implement US2 to make its tests pass

- [X] T189 [US2] Update InitiateSessionCommandHandler to check for linked identities and retrieve previous session summaries
- [X] T190 [US2] Create ConversationSummaryService in Maliev.ChatbotService.Infrastructure/Services/ConversationSummaryService.cs to generate structured JSON summaries per schema
- [X] T191 [US2] Create SessionExpiryBackgroundService in Maliev.ChatbotService.Infrastructure/BackgroundServices/SessionExpiryBackgroundService.cs to close sessions after 24 hours and generate summaries
- [X] T192 [US2] Update SendMessageCommandHandler to include previous session summaries in Gemini context when available
- [X] T193 [US2] Create LinkIdentityCommand in Maliev.ChatbotService.Application/Commands/LinkIdentityCommand.cs
- [X] T194 [US2] Create LinkIdentityCommandHandler in Maliev.ChatbotService.Application/Handlers/LinkIdentityCommandHandler.cs with webhook confirmation logic
- [X] T195 [US2] Add endpoint POST /v1/users/link to SessionsController for identity linking initiation
- [X] T196 [US2] Add XML documentation to all public methods in US2 handlers and services

**Checkpoint**: User Story 2 tests now PASS - cross-platform continuity works

---

## Phase 6: User Story 3 Implementation (Multimodal Input)

**Purpose**: Implement US3 to make its tests pass

- [X] T197 [P] [US3] Create Attachment DTO in Maliev.ChatbotService.Api/Models/Requests/Attachment.cs with size validation (10MB images, 20MB PDFs, 50MB videos)
- [X] T198 [US3] Update SendMessageRequest to include optional attachments array
- [X] T199 [P] [US3] Create IFileValidationService interface in Maliev.ChatbotService.Application/Interfaces/IFileValidationService.cs
- [X] T200 [US3] Implement FileValidationService in Maliev.ChatbotService.Infrastructure/Services/FileValidationService.cs with size and type checks
- [X] T201 [US3] Update GeminiClient to support multimodal requests (images, PDFs, audio, video)
- [X] T202 [US3] Update SendMessageCommandHandler to validate attachments and process multimodal inputs
- [X] T203 [US3] Add error handling for file size limits with clear user-facing error messages
- [X] T204 [US3] Update Message entity metadata to store file URLs and processing information
- [X] T205 [US3] Add XML documentation to multimodal processing methods

**Checkpoint**: User Story 3 tests now PASS - multimodal input processing works

---

## Phase 7: User Story 4 Implementation (Business Context Enforcement)

**Purpose**: Implement US4 to make its tests pass

- [X] T206 [US4] Update SystemInstruction entity to include detailed business constraints and allowed topics
- [X] T207 [US4] Update default system instruction seed data with manufacturing focus and rejection templates
- [X] T208 [US4] Update GeminiClient to enforce system instructions strictly in all API calls
- [X] T209 [US4] Create BusinessConstraintValidator in Maliev.ChatbotService.Application/Validators/BusinessConstraintValidator.cs to validate response compliance
- [X] T210 [US4] Update SendMessageCommandHandler to log off-topic attempts for monitoring
- [X] T211 [US4] Add XML documentation to business constraint enforcement methods

**Checkpoint**: User Story 4 tests now PASS - business boundaries enforced

---

## Phase 8: User Story 5 Implementation (Persistent User Preferences)

**Purpose**: Implement US5 to make its tests pass

- [X] T212 [US5] Create ExtractPreferencesService in Maliev.ChatbotService.Infrastructure/Services/ExtractPreferencesService.cs to identify preference statements (confidence >0.8)
- [X] T213 [US5] Update SendMessageCommandHandler to extract and store user preferences with confidence scores
- [X] T214 [US5] Update InitiateSessionCommandHandler to retrieve and include stored preferences in initial context
- [X] T215 [US5] Create GetUserPreferencesQuery in Maliev.ChatbotService.Application/Queries/GetUserPreferencesQuery.cs
- [X] T216 [US5] Create GetUserPreferencesQueryHandler with pagination in Maliev.ChatbotService.Application/Handlers/GetUserPreferencesQueryHandler.cs
- [X] T217 [US5] Create DeleteUserDataCommand in Maliev.ChatbotService.Application/Commands/DeleteUserDataCommand.cs with scope options (preferences, history, all)
- [X] T218 [US5] Create DeleteUserDataCommandHandler in Maliev.ChatbotService.Application/Handlers/DeleteUserDataCommandHandler.cs with explicit confirmation requirement
- [X] T219 [US5] Create UserDataController in Maliev.ChatbotService.Api/Controllers/V1/UserDataController.cs with GET /v1/users/me/preferences and DELETE /v1/users/me/data
- [X] T220 [US5] Add audit logging for all preference create/update/delete operations
- [X] T221 [US5] Add XML documentation to preference management methods

**Checkpoint**: User Story 5 tests now PASS - persistent preferences work

---

## Phase 9: User Story 6 Implementation (Internal CRM Agent)

**Purpose**: Implement US6 to make its tests pass

- [X] T222 [US6] Create PermissionAuthorizationFilter in Maliev.ChatbotService.Api/Filters/PermissionAuthorizationFilter.cs to check IAM permissions
- [X] T223 [US6] Update InitiateSessionCommandHandler to detect internal agent role and adjust system instructions for CRM operations
- [X] T224 [US6] Create IOperationExecutionService interface in Maliev.ChatbotService.Application/Interfaces/IOperationExecutionService.cs
- [X] T225 [US6] Implement OperationExecutionService in Maliev.ChatbotService.Infrastructure/Services/OperationExecutionService.cs to handle:
  - Quotation status queries (integration with QuotationService API)
  - Order status queries (integration with OrderService API)
  - CRM record updates (integration with CustomerService API)
- [X] T226 [US6] Define OperationResult schema: {success: bool, data: object, error: string?, actions: SuggestedAction[]}
- [X] T227 [US6] Update SendMessageCommandHandler to detect operation intent for internal agents and execute via OperationExecutionService
- [X] T228 [US6] Create structured response formatters for internal CRM data with quick action buttons
- [X] T229 [US6] Add comprehensive audit logging for all internal agent operations to OperationLog
- [X] T230 [US6] Add XML documentation to internal agent assistance methods

**Checkpoint**: User Story 6 tests now PASS - internal CRM agent assistance works

---

## Phase 10: User Story 7 Implementation (Graceful Degradation)

**Purpose**: Implement US7 to make its tests pass

- [X] T231 [US7] Create fallback response templates table in database for: GeminiAPITimeout, GeminiAPIError, RedisUnavailable, ValidationFailure, UnexpectedError
- [X] T232 [US7] Update GeminiClient to implement exponential backoff retry for transient failures
- [X] T233 [US7] Update GeminiClient to return fallback responses after timeout (use ResponseTimeoutService)
- [X] T234 [US7] Update SystemInstructionService to detect Redis unavailability and fallback to direct PostgreSQL reads
- [X] T235 [US7] Add Redis availability health checks with automatic fallback detection and recovery
- [X] T236 [US7] Add comprehensive logging for all fallback scenarios with alerting metadata
- [X] T237 [US7] Update SendMessageCommandHandler to handle schema validation failures with retry and enhanced prompting per FR-002a
- [X] T238 [US7] Add XML documentation to resilience and fallback methods

**Checkpoint**: User Story 7 tests now PASS - graceful degradation works

---

## Phase 11: User Story 8 Implementation (Controlled Web Search)

**Purpose**: Implement US8 to make its tests pass

- [X] T239 [US8] Update SystemInstruction entity to include web search permission flags and domain logging settings
- [X] T240 [US8] Update GeminiClient to include web search capabilities when permitted by system instructions
- [X] T241 [US8] Update SendMessageCommandHandler to log all web search domains accessed to SearchDomainLog table
- [X] T242 [US8] Create response formatter to include source attribution for web search results
- [X] T243 [US8] Add extended timeout handling (30s) for queries involving web searches
- [X] T244 [US8] Add XML documentation to web search methods

**Checkpoint**: User Story 8 tests now PASS - web search capability works

---

## Phase 12: Webhook Integration Implementation

**Purpose**: Implement webhook endpoints to make webhook tests pass

- [X] T245 [P] Create ILineClient interface in Maliev.ChatbotService.Application/Interfaces/ILineClient.cs
- [X] T246 [P] Create IMetaClient interface in Maliev.ChatbotService.Application/Interfaces/IMetaClient.cs for Facebook/Instagram/WhatsApp
- [X] T247 [P] Add Line.Messaging NuGet package to Infrastructure project
- [X] T248 [P] Implement LineClient using Line.Messaging SDK in Maliev.ChatbotService.Infrastructure/ExternalServices/LineClient.cs
- [X] T249 [P] Implement MetaClient using typed HttpClient in Maliev.ChatbotService.Infrastructure/ExternalServices/MetaClient.cs
- [X] T250 [P] Create LineWebhookEvent DTOs in Maliev.ChatbotService.Api/Models/Webhooks/LineWebhookEvent.cs
- [X] T251 [P] Create MetaWebhookEvent DTOs in Maliev.ChatbotService.Api/Models/Webhooks/MetaWebhookEvent.cs
- [X] T252 Create ProcessWebhookCommand in Maliev.ChatbotService.Application/Commands/ProcessWebhookCommand.cs
- [X] T253 Create ProcessWebhookCommandHandler in Maliev.ChatbotService.Application/Handlers/ProcessWebhookCommandHandler.cs with channel adaptation logic
- [X] T254 Create WebhooksController in Maliev.ChatbotService.Api/Controllers/V1/WebhooksController.cs with POST /v1/webhooks/{channel}
- [X] T255 Implement webhook signature verification for LINE and Meta platforms
- [X] T256 Create channel-specific response formatters: LINE Flex Messages, Facebook Generic Templates, Website UI components
- [X] T257 Add XML documentation to webhook processing methods

**Checkpoint**: Webhook tests now PASS - multi-channel integration works

---

## Phase 13: Admin API Implementation

**Purpose**: Implement admin endpoints to make admin tests pass

- [X] T258 [P] Create CreateSystemInstructionRequest DTO in Maliev.ChatbotService.Api/Models/Requests/CreateSystemInstructionRequest.cs
- [X] T259 [P] Create UpdateSystemInstructionRequest DTO in Maliev.ChatbotService.Api/Models/Requests/UpdateSystemInstructionRequest.cs
- [X] T260 [P] Create SystemInstructionDto in Maliev.ChatbotService.Api/Models/Responses/SystemInstructionDto.cs
- [X] T261 [P] Create CreateSystemInstructionCommand in Maliev.ChatbotService.Application/Commands/CreateSystemInstructionCommand.cs
- [X] T262 [P] Create UpdateSystemInstructionCommand in Maliev.ChatbotService.Application/Commands/UpdateSystemInstructionCommand.cs
- [X] T263 [P] Create GetSystemInstructionsQuery in Maliev.ChatbotService.Application/Queries/GetSystemInstructionsQuery.cs
- [X] T264 [P] Create CreateSystemInstructionCommandHandler in Maliev.ChatbotService.Application/Handlers/CreateSystemInstructionCommandHandler.cs
- [X] T265 [P] Create UpdateSystemInstructionCommandHandler in Maliev.ChatbotService.Application/Handlers/UpdateSystemInstructionCommandHandler.cs
- [X] T266 [P] Create GetSystemInstructionsQueryHandler in Maliev.ChatbotService.Application/Handlers/GetSystemInstructionsQueryHandler.cs
- [X] T267 Create AdminController in Maliev.ChatbotService.Api/Controllers/V1/AdminController.cs with GET/POST/PUT/DELETE /v1/admin/instructions
- [X] T268 Add PermissionAuthorizationFilter to AdminController requiring chatbot.instructions.read and chatbot.instructions.write permissions
- [X] T269 Add XML documentation to admin API methods

**Checkpoint**: Admin API tests now PASS - system instruction management works

---

## Phase 14: Business Metrics Implementation (Constitution XII)

**Purpose**: Implement business metrics endpoints to make metrics tests pass

- [X] T270 [P] Create ConversationVolumeMetric counter in Maliev.ChatbotService.Infrastructure/Metrics/ConversationMetrics.cs
- [X] T271 [P] Create ResponseLatencyMetric histogram in Maliev.ChatbotService.Infrastructure/Metrics/ConversationMetrics.cs
- [X] T272 [P] Create GeminiApiSuccessRateMetric gauge in Maliev.ChatbotService.Infrastructure/Metrics/ConversationMetrics.cs
- [X] T273 [P] Create ActiveSessionsCountMetric gauge in Maliev.ChatbotService.Infrastructure/Metrics/ConversationMetrics.cs
- [X] T274 [P] Create UserSatisfactionScoreMetric gauge in Maliev.ChatbotService.Infrastructure/Metrics/ConversationMetrics.cs
- [X] T275 Instrument SendMessageCommandHandler to record conversation_volume and response_latency
- [X] T276 Instrument GeminiClient to record gemini_api_success_rate
- [X] T277 Instrument SessionExpiryBackgroundService to record active_sessions_count
- [X] T278 Ensure all metrics include required tags: service_name=ChatbotService, version, region, environment
- [X] T279 Add XML documentation to metrics instrumentation

**Checkpoint**: Business metrics tests now PASS - metrics exposed per Constitution XII

---

## Phase 15: Messaging Implementation

**Purpose**: Implement event publishing to make messaging tests pass

- [X] T280 [P] Create IEventPublisher interface in Maliev.ChatbotService.Application/Interfaces/IEventPublisher.cs
- [X] T281 Implement MassTransit-based EventPublisher in Maliev.ChatbotService.Infrastructure/Messaging/EventPublisher.cs
- [X] T282 Update InitiateSessionCommandHandler to publish ChatbotSessionCreatedEvent
- [X] T283 Update SessionExpiryBackgroundService to publish ChatbotSessionClosedEvent
- [X] T284 Update SendMessageCommandHandler to publish ChatbotMessageReceivedEvent
- [X] T285 Update RateLimitService to publish ChatbotRateLimitExceededEvent when limit exceeded
- [X] T286 Add XML documentation to event publishing methods

**Checkpoint**: Messaging tests now PASS - events published to RabbitMQ

---

## Phase 16: IAM Integration

**Purpose**: Implement IAM permission registration

- [X] T287 Create ChatbotIAMRegistrationService extending IAMRegistrationService base class in Maliev.ChatbotService.Infrastructure/Services/ChatbotIAMRegistrationService.cs
- [X] T288 Register all chatbot permissions in IAMRegistrationService:
  - chatbot.sessions.create
  - chatbot.sessions.read
  - chatbot.messages.send
  - chatbot.messages.read
  - chatbot.preferences.read
  - chatbot.preferences.write
  - chatbot.preferences.delete
  - chatbot.webhooks.receive
  - chatbot.instructions.read
  - chatbot.instructions.write
- [X] T289 Configure ChatbotIAMRegistrationService as hosted service in Program.cs
- [X] T290 Add XML documentation to IAM integration

**Checkpoint**: IAM integration complete - permissions auto-register on startup

---

## Phase 17: Polish & Validation

**Purpose**: Final constitution compliance verification and production readiness

- [X] T291 [P] Update README.md with project overview, architecture diagram, local setup instructions, and Gemini model configuration guide
- [X] T292 [P] Verify all public methods have XML documentation across all projects (Constitution VIII compliance)
- [X] T293 [P] Run dotnet build with TreatWarningsAsErrors=true and resolve ALL warnings (Constitution VIII compliance) - VERIFIED: 0 warnings, 0 errors
- [X] T294 [P] Audit all .csproj files: Verify ZERO references to AutoMapper, FluentValidation, FluentAssertions (Constitution XIV compliance)
- [X] T295 Code cleanup: Remove unused using statements, apply consistent formatting
- [X] T296 Performance optimization: Add database indexes for common queries (UserProfile platform IDs, ConversationSession expiry, Message session+timestamp)
- [X] T297 Add comprehensive logging with structured log data for all critical operations
- [X] T298 Security hardening: Verify InputValidationService usage, output sanitization, rate limiting enforcement
- [X] T299 Verify quickstart.md instructions work correctly from clean environment
- [X] T300 Add OpenTelemetry tracing spans for all Gemini API calls and external service calls
- [X] T301 Final test run: Execute ALL tests with Testcontainers and verify 100% pass rate (FIXED: RabbitMQ connection string now uses ConnectionStrings:messaging)
- [X] T302 Verify NO docker-compose.yml present (Constitution XVI compliance - final check)
- [X] T303 Verify NO additional .md files in repository root beyond README.md (Constitution IX compliance - final check) (NOTE: GEMINI.md kept per user request)
- [X] T304 Verify .github/CODEOWNERS file present with correct content (Constitution IX compliance)
- [X] T305 Verify Dockerfile in Api/ folder (not root) and uses app user (Constitution X compliance)
- [X] T306 Verify GitHub Actions workflows named ci-develop.yml, ci-staging.yml, ci-main.yml (Constitution XVI compliance)

**Checkpoint**: Constitution compliance verified, all tests passing, production ready ✅

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies - start immediately
- **Phase 2 (Test Authoring)**: Depends on Phase 1 - WRITE ALL TESTS FIRST
- **Phase 3 (Foundation)**: Depends on Phases 1 & 2 - Implement to make foundation tests pass
- **Phases 4-11 (User Stories)**: Depend on Phase 3 - Implement to make user story tests pass
- **Phases 12-13 (Webhooks/Admin)**: Depend on Phase 3 - Can run parallel with user stories
- **Phase 14 (Metrics)**: Depends on Phase 3 - Can run parallel with user stories
- **Phase 15 (Messaging)**: Depends on user story handlers being complete
- **Phase 16 (IAM)**: Depends on API controllers being complete
- **Phase 17 (Polish)**: Depends on all implementation phases complete

### Test-First Mandate (Constitution III)

**CRITICAL**: Phase 2 (Test Authoring) MUST complete BEFORE any Phase 3+ implementation begins. Tests must FAIL initially (Red phase). Implementation makes tests pass (Green phase).

### Parallel Opportunities

- Phase 1: All [P] tasks run in parallel (project creation, configuration files)
- Phase 2: All [P] tests run in parallel (different test files)
- Phase 3: All [P] entities/enums run in parallel, all [P] repositories run in parallel
- Phases 4-11: Different user stories can be worked on by different developers in parallel
- Phases 12-14: Can proceed in parallel with later user stories

---

## Implementation Strategy

### MVP First (Minimum Viable Product)

1. Complete Phase 1: Setup
2. Complete Phase 2: Write ALL tests
3. Complete Phase 3: Foundation
4. Complete Phase 4: User Story 1 only
5. **STOP and VALIDATE**: Run US1 tests - verify they PASS
6. Deploy/demo MVP

### Incremental Delivery

1. Phases 1-3: Setup + Tests + Foundation
2. Phase 4 (US1): Deploy MVP
3. Phase 5 (US2): Deploy cross-platform feature
4. Phase 6 (US3): Deploy multimodal feature
5. Continue incrementally through remaining user stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Phases 1-3 together
2. Once Phase 3 done:
   - Developer A: Phase 4 (US1)
   - Developer B: Phase 5 (US2)
   - Developer C: Phase 12 (Webhooks)
   - Developer D: Phase 13 (Admin API)
3. Stories integrate independently

---

## Notes

- **Constitution III Compliance**: Tests written FIRST (Phase 2) before implementation (Phases 3+)
- **Constitution IV Compliance**: All tests use Testcontainers with real PostgreSQL, Redis, RabbitMQ
- **Constitution VIII Compliance**: TreatWarningsAsErrors=true, zero warnings policy enforced
- **Constitution XII Compliance**: Business metrics endpoints mandatory
- **Constitution XIV Compliance**: NO AutoMapper, FluentValidation, FluentAssertions
- **Constitution XVI Compliance**: NO docker-compose.yml, use Testcontainers
- [P] tasks = different files, can run in parallel
- [Story] label = maps to user story for traceability
- All tasks follow Red-Green-Refactor: Write test (fails) → Implement (passes) → Refactor
- Gemini model: gemini-3-flash, configurable in appsettings.json per user requirement
- Web search: Google Custom Search API with domain restrictions
- Rate limiting: 100 messages per hour per user, HTTP 429 when exceeded
- Commit after each logical group of tasks
- Stop at any checkpoint to validate independently
