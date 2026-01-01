# Data Model

**DbContext**: `ChatbotDbContext`  
**Database**: `chatbot_app_db`  
**Connection String**: `ConnectionStrings:ChatbotDbContext`

## Entities

### UserProfile
*Represents a unified user identity across platforms.*
- `Id`: Guid (PK)
- `InternalUserId`: String (Optional, if linked to internal employee/customer ID)
- `LineUserId`: String (Indexed, Unique when not null)
- `FacebookId`: String (Indexed, Unique when not null)
- `InstagramId`: String (Indexed, Unique when not null)
- `WhatsAppId`: String (Indexed, Unique when not null)
- `Role`: Enum (Customer, InternalAgent)
- `CreatedAt`: DateTimeOffset
- `LastActiveAt`: DateTimeOffset

### ConversationSession
*Represents a 24-hour conversation window.*
- `Id`: Guid (PK)
- `UserProfileId`: Guid (FK -> UserProfile)
- `Channel`: Enum (Website, Line, Facebook, Instagram, WhatsApp, Intranet)
- `StartTime`: DateTimeOffset
- `LastActivityAt`: DateTimeOffset
- `ExpiresAt`: DateTimeOffset (Calculated: LastActivityAt + 24h)
- `Language`: Enum (English, Thai)
- `Status`: Enum (Active, Closed)
- `SummaryId`: Guid (FK -> ConversationSummary, Nullable)

### Message
*A single message within a session.*
- `Id`: Guid (PK)
- `SessionId`: Guid (FK -> ConversationSession)
- `Role`: Enum (User, Assistant, System)
- `Content`: Text
- `ContentType`: Enum (Text, Image, Audio, PDF, Video)
- `MetadataJson`: JsonB (Token usage, processing time, file URLs)
- `CreatedAt`: DateTimeOffset

### ConversationSummary
*Long-term memory of a closed session.*
- `Id`: Guid (PK)
- `SessionId`: Guid (FK -> ConversationSession, Unique)
- `UserProfileId`: Guid (FK -> UserProfile)
- `StructuredSummary`: JsonB - Structured JSON with schema:
  ```json
  {
    "topics": ["string array of main topics discussed"],
    "decisions": ["string array of decisions made"],
    "preferences": ["string array of preferences mentioned"],
    "entities": ["string array of entities referenced (people, places, things)"],
    "intentCategories": ["string array of user intent categories"],
    "unresolvedQuestions": ["string array of questions not fully answered"]
  }
  ```
- `CreatedAt`: DateTimeOffset

### UserMemory
*Persistent facts about a user.*
- `Id`: Guid (PK)
- `UserProfileId`: Guid (FK -> UserProfile)
- `Key`: String (e.g., "MaterialPreference", "DeliveryAddress")
- `Value`: JsonB
- `Confidence`: Double (0.0 - 1.0)
- `SourceMessageId`: Guid (FK -> Message)
- `LastUpdatedAt`: DateTimeOffset

### SystemInstruction
*Configurable persona and rules.*
- `Id`: Guid (PK)
- `Name`: String
- `PersonaDefinition`: Text
- `BusinessConstraints`: Text
- `IsActive`: Boolean
- `Version`: Integer

### IdentityLink
*Association between external platform IDs and internal user profile.*
- `Id`: Guid (PK)
- `UserProfileId`: Guid (FK -> UserProfile)
- `PlatformName`: Enum (Line, Facebook, Instagram, WhatsApp)
- `ExternalPlatformId`: String (Indexed)
- `WebhookConfirmationStatus`: Enum (Pending, Confirmed, Failed)
- `LinkCreatedAt`: DateTimeOffset
- `LastVerifiedAt`: DateTimeOffset

### OperationLog
*System actions and API calls performed by the chatbot.*
- `Id`: Guid (PK)
- `UserProfileId`: Guid (FK -> UserProfile, Nullable)
- `MessageId`: Guid (FK -> Message, Nullable - reference to initiating message)
- `OperationType`: String (e.g., "QuotationCreated", "OrderStatusQueried", "CRMUpdated")
- `OperationParameters`: JsonB (Input parameters for the operation)
- `ExecutionResult`: JsonB (Output result or error details)
- `ExecutedAt`: DateTimeOffset
- `Success`: Boolean

## Relationships
- UserProfile (1) <-> (Many) ConversationSession
- ConversationSession (1) <-> (Many) Message
- ConversationSession (1) <-> (0..1) ConversationSummary
- UserProfile (1) <-> (Many) UserMemory
- UserProfile (1) <-> (Many) IdentityLink
- UserProfile (1) <-> (Many) OperationLog
- Message (1) <-> (0..Many) OperationLog
