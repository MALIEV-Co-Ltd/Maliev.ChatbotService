# Research & Technical Decisions

## 1. Gemini API Integration in .NET
**Decision**: Use `Mscc.GenerativeAI` (Community SDK) or direct REST `HttpClient` with `Microsoft.Extensions.Http.Resilience`.
**Rationale**: 
- There is no official Google-provided "Gemini API" specific SDK for .NET that targets the Google AI Studio (API Key) endpoint directly with the same ease as Python/Node.
- `Mscc.GenerativeAI` is a widely used, standard-compliant wrapper for .NET.
- However, since Maliev uses "standard resilience" patterns (Polly) and "No banned libraries", we might prefer a typed `HttpClient` interacting with the Gemini REST API to have full control and avoid third-party dependencies if `Mscc.GenerativeAI` is considered "external bloat".
- **Final Choice**: **Typed HttpClient** wrapping the Gemini REST API. This strictly adheres to the "No unnecessary libraries" and "Standard Resilience" guidelines of Maliev. It avoids dependency risk on a community package.
- **Alternatives**: `Google.Cloud.AIPlatform.V1` (Vertex AI). This is robust but requires GCP Service Account auth, whereas the spec mentions "Gemini API" which often implies the API Key model. We will stick to the API Key model via REST for simplicity unless Vertex is required.

## 2. Channel Integrations
**Decision**: 
- **LINE**: Use `Line.Messaging` (Pierre3) - The de-facto standard community SDK, widely trusted.
- **WhatsApp/Meta**: Use `WhatsappBusiness.CloudApi` or direct `HttpClient`. Given the complexity of Meta's Graph API, `WhatsappBusiness.CloudApi` is acceptable, but direct `HttpClient` is preferred for strict control. We will use **direct HttpClient** for Meta to minimize dependencies, as the API surface we need (send/receive) is small compared to the full Graph API.
- **Web**: Standard WebSocket (SignalR) or REST polling. Spec implies "chat widget", likely REST-based for simplicity with the chatbot service.

## 3. Internal Service Integration
**Decision**: Use `Maliev.MessagingContracts` for async events and typed `HttpClient` for synchronous queries (Order Status).
**Rationale**: Maliev guidelines explicitly mention `AddHttpClient` with resilience for service-to-service communication.

## 4. Architecture Strategy
**Pattern**: 5-Layer Clean Architecture.
**Reason**: High complexity (Multimodal, State Machine for sessions, External AI integration).
- **Domain**: Pure business logic, Entities (User, Session, Message).
- **Application**: CQRS Handlers (SendMessageCommand, WebhookEventCommand).
- **Infrastructure**: GeminiClient, LineClient, Repository implementations.
- **Api**: Controllers, Webhooks.

## 5. Data & State
- **Session State**: PostgreSQL (Persistent) + Redis (Cache).
- **Vector Search**: Not explicitly requested, but "Context Retrieval" (SC-011b) implies semantic search or just retrieving the JSON summary. Spec says "retrieve previous session summary". We will store summaries as JSONB in PostgreSQL.

## 6. Internal Auth
**Decision**: Use `Maliev.IAMService` via HTTP or gRPC.
**Rationale**: Clarification Q2 confirmed "MALIEV IAM Service".
