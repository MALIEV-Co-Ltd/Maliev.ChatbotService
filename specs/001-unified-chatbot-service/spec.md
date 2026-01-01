# Feature Specification: Unified Chatbot Service

**Feature Branch**: `001-unified-chatbot-service`
**Created**: 2025-12-30
**Status**: Draft
**Input**: User description: "Create a Chatbot Service that acts as a unified conversational intelligence layer for the MALIEV ecosystem. The service must be pluggable into multiple channels including the public website, LINE Messaging API, Meta platforms such as Facebook and Instagram, WhatsApp, and internal intranet applications used for CRM and operations..."

## Clarifications

### Session 2025-12-30

- Q: When a user wants to link their accounts across platforms (e.g., website account to LINE), how should the identity linking process work? → A: User must perform linking by logging in with their identity (e.g., initiating linking by clicking the login button, then a webhook is received to confirm login success).
- Q: When a user sends multiple messages rapidly before previous responses complete, how should the system handle these concurrent messages? → A: Queue messages per session with sequential processing - messages are queued and processed one at a time within each conversation session to maintain context coherence.
- Q: When the Gemini API returns a response that fails schema validation, what should the system do? → A: Retry with stricter prompt then fallback - re-send with enhanced schema enforcement instructions once, then use fallback if still invalid.
- Q: What is the maximum conversation history depth that should be included when calling the Gemini API? → A: Customer conversations should include entire active session history to understand burst messages and context. Session lasts one day, then system stores conversation summary in database. Summary is retrieved in next session for continuity.
- Q: What rate limiting should be applied to individual users to prevent abuse and manage API costs? → A: 100 messages per hour per user - generous limit, prioritizes user experience over abuse prevention.
- Q: What are the maximum file sizes the system should accept for multimodal inputs? → A: 10MB for images, 20MB for PDFs, 50MB for videos - moderate limits balancing user needs and system resources.
- Q: What format should conversation summaries use when sessions close? → A: Structured JSON format - key-value pairs with fields like topics, decisions, preferences, entities mentioned, enabling efficient retrieval and filtering.
- Q: How should domain restrictions be implemented for web searches? → A: No automatic restrictions - rely on system instructions and LLM judgment to refuse inappropriate searches; log all search domains for review.
- Q: What should happen when Redis is unavailable but PostgreSQL remains accessible? → A: Fallback to direct PostgreSQL reads - bypass cache and read directly from PostgreSQL; accept slower response times but maintain functionality.
- Q: When a user asks to "forget" or delete their data through the chatbot, what scope of deletion should be supported? → A: Selective deletion with confirmation - allow users to choose what to delete (specific preferences, recent conversations, or everything) with explicit confirmation.
- Q: What languages should the chatbot support? → A: The chatbot must be strictly bilingual supporting only English (default) and Thai. No other languages are allowed.
- Q: How should the system handle incoming messages when the concurrent conversation limit (1,000) is reached? → A: Queue with "System Busy" notification - acknowledge the message and inform the user of the temporary delay to protect system stability.
- Q: Which authentication source should be used for internal sales agents querying the CRM via the chatbot? → A: MALIEV IAM Service - use the centralized IAM service for RBAC and permission verification to ensure security consistency.
- Q: How long should conversation summaries be retained in the database? → A: 90 days - consistent with the retention period of raw conversation logs to maintain data minimization.
- Q: What should be the maximum response time threshold for queries involving web searches? → A: 30 seconds - provides sufficient time for external search retrieval and reasoning while managing user expectations.
- Q: Should the system perform automated pre-moderation for multimodal inputs (images, audio, video) before sending them to the Gemini API? → A: No, rely on Gemini safety filters - utilize the built-in safety and content filtering capabilities of the Gemini API for all multimodal content.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Customer Initiates Manufacturing Inquiry on Website (Priority: P1)

A potential customer visits the MALIEV public website and opens the chat widget to inquire about manufacturing services. The chatbot responds with structured information about available services, asks qualifying questions about materials and specifications, and provides relevant buttons for next actions like requesting a quotation or viewing portfolio.

**Why this priority**: This is the primary customer acquisition channel and delivers immediate business value by qualifying leads without human intervention.

**Independent Test**: Can be fully tested by opening the website chat widget, asking about manufacturing services, and verifying that responses include structured buttons, service descriptions, and lead qualification questions.

**Acceptance Scenarios**:

1. **Given** a visitor on the MALIEV website, **When** they open the chat widget and ask "What manufacturing services do you offer?", **Then** the chatbot responds in English (default) with a structured list of services with clickable buttons for each category
2. **Given** a visitor starts a conversation in Thai, **When** they ask about manufacturing services, **Then** the chatbot detects Thai language and responds in Thai maintaining language consistency throughout the session
3. **Given** an ongoing conversation about metal fabrication, **When** the visitor asks a follow-up question, **Then** the chatbot maintains conversation context and responds in the same language the conversation started in
4. **Given** a visitor asks about pricing, **When** the chatbot provides information, **Then** the response includes structured buttons to "Request Quote" and "View Portfolio"
5. **Given** a visitor sends a message in Chinese or another unsupported language, **When** the chatbot receives it, **Then** it responds in English with a polite message indicating it only supports English and Thai

---

### User Story 2 - Cross-Platform Conversation Continuity (Priority: P1)

A customer starts a conversation on the website about a custom manufacturing project. Later, they contact MALIEV via LINE and mention the previous conversation. The chatbot recognizes the linked identity and continues the conversation with full context, recalling their project requirements and previous discussion points.

**Why this priority**: This is critical for customer experience and operational efficiency - prevents customers from repeating information and demonstrates MALIEV's integrated service approach.

**Independent Test**: Can be fully tested by initiating a conversation on one channel with a logged-in user, then continuing on another channel with the same user identity, and verifying that conversation history is preserved.

**Acceptance Scenarios**:

1. **Given** an authenticated user discussing project requirements on the website, **When** they send a LINE message to MALIEV's official account with their linked LINE ID, **Then** the chatbot acknowledges the previous conversation and asks if they want to continue the discussion
2. **Given** a user who switched from website to LINE, **When** they reference "the project we discussed earlier", **Then** the chatbot correctly recalls specific details from the website conversation
3. **Given** a user conversing in English on the website, **When** they continue on LINE, **Then** the chatbot maintains English language unless the user explicitly switches
4. **Given** a customer with a conversation that ended 3 days ago (previous session closed), **When** they start a new conversation, **Then** the chatbot retrieves the previous session summary and references past discussions appropriately

---

### User Story 3 - Multimodal Input Processing (Priority: P2)

A customer sends images of a product they want manufactured through LINE. The chatbot analyzes the images, identifies the type of manufacturing process needed, asks clarifying questions about dimensions and materials, and provides a preliminary feasibility assessment.

**Why this priority**: Multimodal capability differentiates MALIEV's service and significantly reduces friction in the quotation process by allowing customers to show rather than describe.

**Independent Test**: Can be tested by sending product images through any supported channel and verifying that the chatbot acknowledges the image, provides relevant analysis, and asks appropriate follow-up questions.

**Acceptance Scenarios**:

1. **Given** a customer conversation on LINE, **When** they upload an image of a metal part, **Then** the chatbot analyzes the image and responds with a structured message identifying the type of manufacturing process and key questions about specifications
2. **Given** a customer uploads a PDF technical drawing, **When** the chatbot processes it, **Then** it extracts relevant dimensions and material specifications and confirms understanding with the customer
3. **Given** a customer sends an audio message describing their requirements, **When** the chatbot processes it, **Then** it transcribes the request, summarizes the requirements in text, and asks for confirmation

---

### User Story 4 - Business Context Enforcement (Priority: P2)

A customer asks the chatbot about unrelated topics like weather forecasts or restaurant recommendations. The chatbot politely declines and redirects the conversation to MALIEV's manufacturing services, explaining its scope of assistance.

**Why this priority**: Prevents resource waste on off-topic conversations and maintains professional brand image by keeping conversations focused on business value.

**Independent Test**: Can be tested by asking clearly off-topic questions and verifying that the chatbot provides consistent, professional rejection responses while suggesting relevant topics.

**Acceptance Scenarios**:

1. **Given** an active conversation, **When** a customer asks "What's the weather like?", **Then** the chatbot responds "I specialize in MALIEV's manufacturing services. I can help you with inquiries about metal fabrication, custom manufacturing, quotations, and project consultations. How can I assist you with manufacturing needs?"
2. **Given** a customer asks about competitors' services, **When** the chatbot receives this request, **Then** it politely declines and redirects to MALIEV's unique capabilities
3. **Given** repeated off-topic requests, **When** the pattern is detected, **Then** the chatbot maintains the same professional boundary without escalating tone

---

### User Story 5 - Persistent User Preferences (Priority: P2)

A returning customer who previously discussed stainless steel fabrication starts a new conversation. The chatbot proactively recalls their preferred material (stainless steel), previously used delivery address, and preferred communication language (Thai), offering to use these preferences to streamline the current inquiry.

**Why this priority**: Personalizes the experience and reduces conversation length, improving both customer satisfaction and operational efficiency.

**Independent Test**: Can be tested by having a user complete multiple conversations with explicit preferences stated, then starting a new conversation and verifying that the chatbot offers to apply remembered preferences.

**Acceptance Scenarios**:

1. **Given** a returning customer who previously specified "stainless steel 304" as preferred material, **When** they ask about a new project, **Then** the chatbot suggests "I see you've worked with stainless steel 304 before. Would you like to use the same material for this project?"
2. **Given** a customer who previously provided their delivery address, **When** they request a quotation, **Then** the chatbot asks "Should I use your address at [saved address] for this quotation?"
3. **Given** a customer who always communicates in Thai, **When** they start a new conversation, **Then** the chatbot automatically begins in Thai
4. **Given** a customer asks "delete my saved address", **When** they have one address stored, **Then** the chatbot asks for confirmation "Are you sure you want to delete your saved address at [address]? This cannot be undone." and deletes only upon explicit confirmation
5. **Given** a customer asks "forget everything about me", **When** this request is received, **Then** the chatbot presents deletion options (specific preferences, conversation history, or complete profile) and requires confirmation before proceeding

---

### User Story 6 - Internal CRM Agent Assistance (Priority: P3)

A MALIEV sales agent using the internal intranet needs to quickly check the status of a customer's quotation. They ask the chatbot "What's the status of quotation Q-2025-1234?" and receive a structured response with current status, pending actions, and quick action buttons to "Send Reminder" or "View Full Details."

**Why this priority**: Improves internal operational efficiency but is less critical than customer-facing features. Still valuable for reducing internal query time.

**Independent Test**: Can be tested by a logged-in internal user querying system information and verifying that responses include both data and actionable operations appropriate to the intranet interface.

**Acceptance Scenarios**:

1. **Given** an authenticated sales agent, **When** they ask "Status of Q-2025-1234", **Then** the chatbot returns structured data showing quotation status, customer name, pending actions, and operation buttons
2. **Given** an agent asks "Show me all pending quotations for customer ABC Corp", **When** the chatbot processes this request, **Then** it queries internal systems and returns a structured list with quick actions
3. **Given** an agent requests "Send reminder for Q-2025-1234", **When** the chatbot executes this action, **Then** it performs the operation via internal APIs and confirms completion with structured feedback

---

### User Story 7 - Graceful Degradation on External Dependency Failure (Priority: P3)

During a Gemini API outage, a customer sends a message through LINE. Instead of an error message, they receive a polite response: "I'm experiencing temporary technical difficulties. Your message has been logged, and a MALIEV team member will respond shortly. For urgent inquiries, please call [phone number]." Similarly, when Redis cache is unavailable, the system continues functioning with direct database reads, maintaining service availability with slightly slower response times.

**Why this priority**: Maintains professional image and customer trust during failures, though it's a fallback scenario rather than primary functionality.

**Independent Test**: Can be tested by simulating external dependency failures (Gemini API, Redis) and verifying that the system continues functioning with appropriate degradation strategies.

**Acceptance Scenarios**:

1. **Given** the Gemini API is unavailable, **When** a customer sends any message, **Then** the chatbot responds with a predefined fallback message offering alternative contact methods
2. **Given** a timeout from the Gemini API after 10 seconds, **When** this occurs, **Then** the system returns the fallback response and logs the failure for monitoring
3. **Given** API response fails schema validation, **When** this occurs, **Then** the system retries once with enhanced schema enforcement instructions and falls back to predefined response if retry also fails
4. **Given** Redis cache is unavailable, **When** a customer sends a message, **Then** the system retrieves system instructions directly from PostgreSQL and processes the message with acceptable performance degradation

---

### User Story 8 - Controlled Web Search for Technical Specs (Priority: P3)

A customer asks about the properties of a specific steel alloy grade. The chatbot's system instructions permit controlled web searches for technical specifications. The chatbot performs a domain-restricted search, retrieves authoritative information, and provides a structured response with source attribution.

**Why this priority**: Enhances chatbot knowledge beyond static training data, but requires careful implementation to prevent misuse, making it lower priority than core conversational features.

**Independent Test**: Can be tested by asking questions that require external knowledge, verifying that search is performed only when permitted by system instructions, and confirming that results are properly attributed and domain-restricted.

**Acceptance Scenarios**:

1. **Given** a customer asks "What are the properties of ASTM A36 steel?", **When** system instructions permit web search for technical specs, **Then** the chatbot performs a web search, logs the accessed domains, and returns structured information with sources cited
2. **Given** a customer asks about competitor pricing, **When** this triggers a search consideration, **Then** the chatbot refuses based on business constraint rules defined in system instructions regardless of search capability
3. **Given** web search is disabled in system instructions, **When** a customer asks a question requiring external data, **Then** the chatbot responds based only on trained knowledge and indicates uncertainty if applicable
4. **Given** the chatbot performs a web search, **When** the search completes, **Then** all accessed domains are logged for monitoring and review

---

### Edge Cases

- What happens when a user sends messages in multiple languages within the same conversation (e.g., starts in English, then sends a Thai message)?
- What happens when a user sends a message in an unsupported language (e.g., Chinese, Japanese, French)?
- How does the system handle session expiry if a user sends a message exactly at the 24-hour boundary?
- What happens if conversation summary generation fails when closing a session? → System logs error, marks session as closed anyway, creates empty summary placeholder with error flag, and schedules async retry
- What happens if a stored conversation summary JSON is corrupted or cannot be parsed when retrieving for a new session? → System logs warning, treats as no-summary-available, continues conversation without previous context
- How does the system handle identity linking when a user has multiple LINE accounts or multiple Facebook accounts and attempts to link more than one to the same internal profile?
- What happens when a user explicitly asks to "forget" their preferences or conversation history?
- How should the chatbot handle ambiguous deletion requests (e.g., "delete my address" when user has multiple addresses stored)?
- What confirmation language and flow should be used to ensure users understand the consequences of deletion?
- How does the system behave when a user sends extremely long messages exceeding typical token limits?
- What happens when a user uploads a file that exceeds the size limits (e.g., 15MB image, 30MB PDF)?
- What happens when a user uploads a file that cannot be processed (corrupted file, unsupported format)?
- How does the system indicate to users that their rapid-fire messages are queued and being processed sequentially (e.g., typing indicators, queue position)?
- What is the expected response time degradation when operating in Redis fallback mode with direct PostgreSQL reads?
- How does the system detect Redis recovery and resume normal caching operations?
- How does the system handle language detection when a message contains mixed-language content?
- How does the system handle incoming messages when the concurrent conversation limit (1,000) is reached?
- What happens when a user denies permission to store their preferences or conversation history?
- How does the chatbot respond when asked to perform actions that would require authentication but the user is not authenticated?
- What happens when a user reaches the 100 messages per hour rate limit - should queued messages be rejected or delayed until the next hour window? → Messages exceeding rate limit are REJECTED immediately with HTTP 429 (not queued) to prevent resource exhaustion. Response includes Retry-After header indicating when window resets.
- What happens if the LLM performs a web search on an inappropriate domain despite system instruction restrictions?
- How frequently should logged web search domains be reviewed for compliance and safety?
- What happens if Gemini API safety filters reject a multimodal input that the user considers benign?

## Requirements *(mandatory)*

### Functional Requirements

#### Core Conversational Intelligence

- **FR-001**: System MUST process and respond to text messages from all supported channels (website, LINE, Facebook, Instagram via Meta platform integration, WhatsApp, internal intranet)
- **FR-002**: System MUST enforce structured response schemas for all LLM outputs to ensure machine-readable formats
- **FR-002a**: System MUST validate all LLM responses against predefined schemas and, upon validation failure, retry once with enhanced schema enforcement instructions before falling back to predefined response
- **FR-003**: System MUST prevent free-form text responses that lack predefined structure
- **FR-004**: System MUST support multimodal input processing including text, audio, images, videos, and PDF documents
- **FR-004a**: System MUST enforce file size limits of 10MB for images, 20MB for PDFs, and 50MB for videos to balance user needs with system resources
- **FR-005**: System MUST use Gemini API with gemini-3-flash model as the primary conversational AI engine

#### System Instructions and Persona Management

- **FR-006**: System MUST store system instructions and persona configurations securely in PostgreSQL
- **FR-007**: System MUST cache system instructions in Redis for efficient retrieval
- **FR-007a**: System MUST fallback to direct PostgreSQL reads when Redis is unavailable, accepting slower response times while maintaining functionality
- **FR-008**: System MUST never expose system instructions, business rules, or behavioral constraints to end users
- **FR-009**: System MUST support runtime contextual augmentation of system instructions based on user intent and conversation state
- **FR-010**: System MUST allow injection of domain-specific knowledge (manufacturing processes, pricing logic, MALIEV business rules) into conversation context

#### Channel Adaptation

- **FR-011**: System MUST adapt output format to match the capabilities of each channel (buttons for website, templates for LINE, quick replies for Meta platforms)
- **FR-012**: System MUST maintain consistent core conversational behavior across all channels
- **FR-013**: System MUST support channel-specific rich response formats (LINE Flex Messages, Facebook Generic Templates, website chat UI components)

#### Conversation Continuity and Identity

- **FR-014**: System MUST maintain conversation history across sessions within the same channel
- **FR-014a**: System MUST queue incoming messages per conversation session and process them sequentially to maintain message order and context coherence
- **FR-014b**: System MUST include the entire active session history when calling the Gemini API to maintain full context for burst messages and multi-turn conversations
- **FR-014c**: System MUST define an active session duration of one day (24 hours from last activity), after which the session is considered closed
- **FR-014d**: System MUST generate and store a conversation summary in structured JSON format in the database when a session closes (after one day of inactivity), including fields for topics discussed, decisions made, preferences mentioned, and entities referenced
- **FR-014e**: System MUST retrieve and include previous session summaries when initiating a new session with the same user to maintain long-term conversation continuity
- **FR-015**: System MUST support cross-platform conversation continuity for authenticated or identity-linked users
- **FR-016**: System MUST provide an identity-linking mechanism where users initiate linking by clicking a login button on the channel, authenticate with their credentials, and the system receives a webhook confirmation of successful login to associate the external platform identifier with their internal user profile
- **FR-017**: System MUST restrict cross-platform memory and personalization to authenticated or explicitly linked users only
- **FR-018**: System MUST allow unauthenticated users to have single-session conversations without persistent memory

#### Persistent User Memory

- **FR-019**: System MUST store business-relevant user preferences in structured form (preferred services, materials, colors, addresses, names, language preferences)
- **FR-020**: System MUST retrieve stored user preferences for future conversations to improve relevance
- **FR-021**: System MUST make memory storage intentional and explicit (not automatic for all mentioned information) - user must explicitly confirm preference storage, OR LLM must detect explicit preference statements with confidence >0.8
- **FR-022**: System MUST provide audit trails for user memory storage and retrieval
- **FR-023**: System MUST align memory storage with privacy and data governance policies
- **FR-024**: System MUST allow users to view, modify, or selectively delete their stored data through chatbot conversation commands, offering granular choices (specific preferences, recent conversations, or complete profile) with explicit confirmation flows to prevent accidental deletion
- **FR-025**: System MUST retain user preference data (materials, addresses, names, language preferences, etc.) indefinitely unless explicitly deleted by the user or required by data governance policies

#### Language Consistency

- **FR-026**: System MUST detect the language of the initial user message in a conversation, supporting only English and Thai
- **FR-026a**: System MUST default to English when language cannot be reliably detected or when messages are sent in unsupported languages
- **FR-027**: System MUST maintain the detected language for all subsequent responses in that conversation
- **FR-028**: System MUST only switch language when the user explicitly switches language in their messages between English and Thai
- **FR-029**: System MUST enforce language consistency at both system-instruction and response-validation layers
- **FR-030**: System MUST support exactly two languages: English (default) and Thai, with no support for other languages
- **FR-030a**: System MUST refuse or redirect requests in languages other than English and Thai with a polite message in English

#### Business Constraints and Safety

- **FR-031**: System MUST restrict responses to manufacturing-related inquiries and MALIEV-specific services
- **FR-032**: System MUST refuse or redirect out-of-scope requests in a predictable, polite manner
- **FR-033**: System MUST enforce business boundaries defined in system instructions (e.g., not discussing competitor services, not providing non-manufacturing advice)
- **FR-034**: System MUST perform controlled web searches only when explicitly permitted by system instructions, using Google Custom Search API with domain restrictions
- **FR-035**: System MUST rely on system instructions and LLM judgment to enforce domain and topic restrictions for web searches, with no automatic technical domain filtering
- **FR-035a**: System MUST log all web search domains accessed by the chatbot for monitoring and review purposes
- **FR-035b**: System MUST rely on the Gemini API's built-in safety filters and content moderation for all multimodal inputs (images, audio, video) instead of implementing a separate pre-moderation layer.
- **FR-036**: System MUST validate and sanitize all user inputs to prevent injection attacks
- **FR-036a**: System MUST enforce rate limiting of 100 messages per hour per user to prevent abuse while prioritizing legitimate user experience
- **FR-036b**: System MUST, when the concurrent conversation limit is reached, queue new messages and notify users that the system is temporarily busy to ensure stability and acknowledge the request.

#### Resilience and Error Handling

- **FR-037**: System MUST degrade gracefully when Gemini API is unavailable, times out, or returns errors
- **FR-038**: System MUST return user-appropriate fallback responses without exposing internal errors or system details
- **FR-039**: System MUST log all API failures, timeouts, and errors for observability
- **FR-040**: System MUST maintain stable and predictable user experience during external dependency failures
- **FR-041**: System MUST implement retry logic with exponential backoff for transient API failures
- **FR-042**: System MUST define maximum response time thresholds and trigger fallback responses when exceeded (default: 10 seconds)
- **FR-042b**: System MUST apply an extended maximum response time threshold of 30 seconds specifically for queries requiring web searches to accommodate external network latency.
- **FR-042c**: System MUST log Redis unavailability incidents and automatically fallback to direct database reads without service interruption

#### Integration and Microservices

- **FR-043**: System MUST integrate with MALIEV's existing microservices for data retrieval and operations
- **FR-044**: System MUST support executing system actions (creating quotations, retrieving order status, updating CRM records) through internal APIs when requested by authorized users
- **FR-045**: System MUST return structured operation results that can be embedded in channel-appropriate responses, using the OperationResult schema: {success: bool, data: object, error: string?, actions: SuggestedAction[]}
- **FR-046**: System MUST authenticate internal users via the MALIEV IAM Service before allowing access to operational commands or CRM data.

#### Observability and Compliance

- **FR-047**: System MUST log all conversations and store conversation summaries with channel identifier, UserProfile identifier (if authenticated), timestamps, and content with a retention period of 90 days for both raw logs and summaries.
- **FR-048**: System MUST track and expose metrics for conversation volume, response latency, API success/failure rates, and user satisfaction indicators
- **FR-049**: System MUST implement audit logging for all user memory operations (create, read, update, delete)
- **FR-050**: System MUST support conversation export for compliance and quality assurance purposes

### Key Entities

- **UserProfile**: Represents a unified user identity across platforms; contains internal user ID, linked external platform identifiers (LINE ID, Facebook ID, etc.), authentication status, user role (customer, internal agent), and creation timestamp

- **UserMemory**: Represents stored user preferences and business-relevant information; contains UserProfile reference, memory category (material preference, address, language, etc.), structured memory value, confidence score, last updated timestamp, and audit trail

- **ConversationSession**: Represents a continuous conversation thread with one-day duration; contains session ID, UserProfile reference, channel identifier, conversation start time, last activity time, session expiry time (24 hours from last activity), language, conversation state, message history, and session status (active/closed)

- **ConversationSummary**: Represents a condensed summary of a closed session for long-term context retention stored in structured JSON format; contains summary ID, session reference, UserProfile reference, JSON summary data (with fields: topics discussed, decisions made, preferences mentioned, entities referenced, intent categories, unresolved questions), creation timestamp, and summary generation method

- **Message**: Represents a single message in a conversation; contains message ID, session reference, sender role (user/assistant), message timestamp, content (text, image URLs, audio URLs, etc.), content type, structured response schema, and processing metadata

- **SystemInstruction**: Represents dynamic chatbot behavior configuration; contains instruction ID, instruction name, persona definition, business constraints, allowed capabilities (web search, API operations), domain restrictions, target user roles, version, and activation status

- **ChannelConfiguration**: Represents platform-specific settings; contains channel identifier (website, LINE, Facebook, etc.), response format templates, authentication requirements, rate limits (100 messages per hour per user), and feature flags

- **IdentityLink**: Represents association between external platform IDs and internal UserProfile established through login-based webhook confirmation; contains link ID, UserProfile reference, platform name, external platform ID, webhook confirmation status, link creation timestamp, and last verified timestamp

- **OperationLog**: Represents system actions and API calls performed by the chatbot; contains log ID, UserProfile reference, operation type, operation parameters, execution result, timestamp, and initiating message reference

## Success Criteria *(mandatory)*

### Measurable Outcomes

#### User Experience and Engagement

- **SC-001**: 90% of customer inquiries receive structured responses within 3 seconds under normal operating conditions (<500 concurrent sessions, <70% CPU, Gemini API p99 latency <2s, Redis available)
- **SC-002**: 80% of users who start a conversation on one channel and continue on another report that the chatbot successfully remembered their previous context (measured through follow-up surveys)
- **SC-003**: Users can complete a quotation request conversation in under 5 minutes when all required information is provided
- **SC-004**: 95% of multimodal inputs (images, PDFs, audio) within size limits are successfully processed and acknowledged within 5 seconds
- **SC-004a**: Users uploading files exceeding size limits receive clear error messages with size limit information within 1 second

#### Business Value

- **SC-005**: 60% of manufacturing-related inquiries are fully handled by the chatbot without requiring human escalation
- **SC-006**: Response time for internal agents querying system information decreases by 50% compared to manual system queries
- **SC-007**: Customer satisfaction score for chatbot interactions reaches 4.0 out of 5.0 within three months of deployment

#### System Reliability and Performance

- **SC-008**: System maintains 99.5% uptime for core conversational features across all channels
- **SC-009**: During Gemini API failures, 100% of users receive appropriate fallback messages within 2 seconds
- **SC-009a**: During Redis unavailability, system continues to function using PostgreSQL fallback with response times degraded by no more than 50% (compared to median response time with Redis available over previous 24 hours)
- **SC-010**: System successfully handles 1,000 concurrent conversations across all channels without degradation
- **SC-010a**: Rate limiting enforcement is accurate within 5% tolerance (users should not be able to exceed 105 messages per hour)
- **SC-011**: Conversation history is preserved with 100% accuracy for authenticated users across platform switches
- **SC-011a**: 95% of conversation summaries are successfully generated in valid JSON format and stored when sessions close after 24 hours of inactivity
- **SC-011b**: Users returning after session expiry report that the chatbot successfully recalls key points from previous conversations (measured through 90% accuracy in context retrieval surveys)

#### Language and Persona Consistency

- **SC-012**: 99% of conversations maintain language consistency throughout the session without unintended language drift
- **SC-012a**: 100% of messages in unsupported languages are detected and responded to with English default language with appropriate redirection message
- **SC-013**: 95% of out-of-scope requests receive consistent, professional boundary-setting responses aligned with MALIEV's brand voice

#### Security and Compliance

- **SC-014**: Zero incidents of system instructions or business rules being exposed to end users
- **SC-015**: 100% of user memory operations are auditable with complete timestamps and change tracking
- **SC-015a**: 100% of user-initiated deletion requests are confirmed before execution and logged with full audit trail including deletion scope and timestamp
- **SC-016**: All authentication requirements are enforced with zero unauthorized access to operational commands or cross-platform memory
- **SC-016a**: 100% of web search operations are logged with complete domain information and timestamps for monitoring and compliance review

## Assumptions

1. **Infrastructure**: Assumes MALIEV has existing PostgreSQL and Redis infrastructure that can be extended for chatbot data storage and caching
2. **Authentication**: Assumes MALIEV has an existing authentication system that can provide user identity tokens for linking external platform IDs
3. **API Access**: Assumes MALIEV has access to Gemini API with appropriate quotas and rate limits for production usage
4. **Channel Setup**: Assumes MALIEV has or will establish official accounts/integrations with LINE, Meta platforms, and WhatsApp with necessary API credentials
5. **Microservices**: Assumes MALIEV's internal microservices expose APIs that can be called by the chatbot service for operations like quotation creation and order status retrieval
6. **Compliance**: Standard conversation logs are retained for 90 days; user preference data is retained indefinitely unless explicitly deleted by the user or required by data governance policies
7. **Language Support**: System supports exactly two languages: English (default) and Thai only; no additional languages will be supported to maintain service quality and consistency
8. **Response Format**: Assumes each channel has documented APIs/SDKs for sending structured responses (buttons, cards, templates)
9. **Monitoring**: Assumes MALIEV has observability infrastructure (logging, metrics, alerting) that the chatbot service can integrate with
10. **Privacy Framework**: Assumes MALIEV has a privacy policy framework that defines permissible user data storage and usage; chatbot will align with these policies
