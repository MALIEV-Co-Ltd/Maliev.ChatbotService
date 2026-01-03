# Data Model: Categorized System Instructions

## Updated Entity: SystemInstruction
- `Id`: Guid (PK)
- `Name`: String
- `Category`: Enum (Core, Topic)
- `TopicKey`: String (Nullable, Indexed)
- `Priority`: Integer (Default: 0)
- `PersonaDefinition`: Text
- `BusinessConstraints`: Text
- `AllowedTopics`: String (Comma-separated)
- `RejectionTemplates`: Text (JSON)
- `IsActive`: Boolean
- `Version`: Integer
- `EnableWebSearch`: Boolean
- `LogSearchDomains`: Boolean

## New Entity: KnowledgeBase
- `Id`: Guid (PK)
- `TopicKey`: String (Indexed, links to SystemInstruction.TopicKey)
- `FactKey`: String (e.g., "Pricing-Standard")
- `Content`: Text (The factual data)
- `Metadata`: JsonB (Extra attributes)
- `CreatedAt`: DateTimeOffset
- `UpdatedAt`: DateTimeOffset

## Enums
### SystemInstructionCategory
- `Core` = 1
- `Topic` = 2

## Relationships
- `SystemInstruction` is a lookup table.
- `KnowledgeBase` entries are retrieved based on `TopicKey` detected during conversation.
- `Message.MetadataJson` will store:
  ```json
  {
    "intent": "3D-Scanning",
    "confidence": 0.95,
    "injectedTopicKeys": ["3D-Scanning"],
    "injectedKnowledgeIds": ["guid-1", "guid-2"]
  }
  ```
