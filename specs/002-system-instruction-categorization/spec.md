# Feature Specification: System Instruction Categorization and Dynamic Injection

**Feature Branch**: `002-system-instruction-categorization`  
**Created**: 2026-01-03  
**Status**: Draft  
**Input**: User description: "Enhance the SystemInstruction feature to support categorization and dynamic injection. We need to distinguish between 'Core' persona instructions (always included) and 'Topic-specific' instructions (injected based on user intent). The system should be able to fetch supplemental knowledge for specific topics like 'Pricing' or '3D Scanning' during the conversation flow to manage token limits and provide specialized expertise."

## Clarifications

### Session 2026-01-03

- Q: How should the system identify the user's intent to trigger the injection of Topic-specific instructions and Knowledge Base facts? → A: LLM-based classification using a specialized `gemini-2.5-flash-lite` model, configurable via `appsettings.json`.
- Q: When merging Core persona instructions with Topic-specific instructions, how should the system handle potential contradictions? → A: Core instructions take precedence for behavioral and safety rules; Topic instructions take precedence for domain-specific facts.
- Q: How should the system retrieve facts from the Knowledge Base? → A: SQL/Direct lookup based on the detected `TopicKey`.
- Q: How should the system manage total prompt size when multiple instructions or facts are retrieved? → A: Priority-based truncation using the `Priority` and `Intent Confidence` properties until a safe token threshold is reached.
- Q: How should the system log and surface the results of the Intent Classification and Dynamic Injection process? → A: Store detected intent, confidence, and source IDs in the `MetadataJson` field of the generated message.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Configures Categorized Instructions (Priority: P1)

As a system administrator, I want to categorize system instructions into 'Core' and 'Topic-Specific' so that the chatbot can maintain a consistent persona while being able to scale its knowledge for specific domains without hitting token limits.

**Why this priority**: Foundation for the entire feature. Categorization is required before any dynamic injection can occur.

**Independent Test**: Can be fully tested by using the Admin API to create instructions with categories and verifying they are stored correctly in the database.

**Acceptance Scenarios**:

1. **Given** an authenticated admin, **When** they create a system instruction with category 'Core', **Then** the system marks it as a global instruction that will be included in all sessions.
2. **Given** an authenticated admin, **When** they create a system instruction with category 'Topic' and topic key '3D-Scanning', **Then** the system stores it as a specialized instruction that only activates when the intent matches '3D-Scanning'.

---

### User Story 2 - Intent-Based Dynamic Instruction Injection (Priority: P1)

As a customer, when I ask about a specific service like "3D Scanning", I want the chatbot to provide expert-level information based on specialized instructions, while still maintaining its core persona.

**Why this priority**: Core value of the feature. This enables the "Expert" behavior requested by the user.

**Independent Test**: Can be tested by sending a message about a specific topic and verifying the LLM response contains details only found in the topic-specific instruction set.

**Acceptance Scenarios**:

1. **Given** a user message "Tell me about your 3D scanning service", **When** the system detects the '3D-Scanning' intent, **Then** it fetches both 'Core' and '3D-Scanning' instructions and merges them for the LLM prompt.
2. **Given** a user message "Hello", **When** no specific topic intent is detected, **Then** only the 'Core' instructions are sent to the LLM to save tokens.

---

### User Story 3 - Knowledge Base Fact Retrieval (Priority: P2)

As a customer, when I ask for specific facts like "What is the price for on-site 3D scanning?", I want the chatbot to retrieve accurate information from a specialized knowledge base.

**Why this priority**: Addresses the specific "Pricing" and "Facts" lookup requirement.

**Independent Test**: Can be tested by asking for pricing information and verifying the response contains accurate data from the knowledge base table.

**Acceptance Scenarios**:

1. **Given** a user query about pricing, **When** the intent is 'PricingLookup', **Then** the system retrieves the relevant pricing snippets from the Knowledge Base and injects them into the prompt as "Context".

---

### Edge Cases

- **Multiple Intents**: What happens if a user asks about 3D Scanning AND CNC Machining in one message? → System should merge both topic instructions if confidence is high, or prioritize the first one.
- **Conflict in Instructions**: What if a topic instruction contradicts a core instruction? → Core instructions MUST take precedence for persona and safety; topic instructions take precedence for factual domain knowledge.
- **Missing Topic Instruction**: What if an intent is detected but no corresponding instruction exists? → Fallback to Core instructions only and inform user of limited specialized knowledge.
- **Token Overflow**: What if the combined Core + multiple Topic instructions exceed the token limit? → System must prioritize Core, then the highest confidence topic instruction, and truncate or omit others.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a 'Category' property for `SystemInstruction` (Core, Topic, Knowledge).
- **FR-002**: System MUST support a 'TopicKey' property for `SystemInstruction` to uniquely identify specialized domains.
- **FR-003**: System MUST identify user intent from incoming messages to determine which Topic-specific instructions to inject.
- **FR-004**: System MUST inject 'Core' instructions into EVERY LLM request.
- **FR-005**: System MUST dynamically merge 'Core' and relevant 'Topic' instructions into a single system prompt for the Gemini API.
- **FR-005a**: System MUST enforce instruction precedence: Core instructions override behavior/safety; Topic instructions override domain facts.
- **FR-005b**: System MUST manage token limits by including Topic instructions and facts based on their `Priority` and `Intent Confidence` until a safe threshold is reached.
- **FR-006**: System MUST support a `KnowledgeBase` entity for storing granular facts (like specific pricing tiers).
- **FR-006a**: System MUST retrieve facts from the `KnowledgeBase` using direct SQL/Indexed lookup based on the detected `TopicKey`.
- **FR-007**: System MUST use a caching strategy that caches merged results or individual instruction components to maintain performance.
- **FR-008**: System MUST provide an Admin API to manage categorized instructions and knowledge base entries.
- **FR-009**: System MUST store intent classification results (intent, confidence) and context injection details (Source IDs) in the `MetadataJson` of the generated message for observability.

### Key Entities

- **SystemInstruction (Updated)**:
  - `Category`: Enum (Core, Topic)
  - `TopicKey`: String (Nullable, e.g., "3D-Scanning", "CNC-Machining")
  - `Priority`: Integer (Determines injection order if multiple topics match)
- **KnowledgeBase**:
  - `Id`: Guid
  - `TopicKey`: String (Links to SystemInstruction topics)
  - `FactKey`: String (e.g., "OnSitePrice")
  - `Content`: Text (The actual factual data)
  - `Metadata`: JsonB

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of LLM requests include the 'Core' persona definition.
- **SC-002**: Topic-specific instructions are injected ONLY when the corresponding intent is detected with >0.7 confidence.
- **SC-003**: Average prompt token count for general greetings is reduced by at least 30% compared to a "one-size-fits-all" instruction approach.
- **SC-004**: Bot successfully retrieves and states the correct pricing from the Knowledge Base for "on-site 3D scanning" in 95% of test queries.