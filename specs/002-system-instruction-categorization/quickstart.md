# Quickstart: Categorized System Instructions

## Local Development Setup

1. **Database Migration**:
   ```bash
   dotnet ef migrations add AddCategorizedInstructions --project Maliev.ChatbotService.Infrastructure
   dotnet ef database update --project Maliev.ChatbotService.Infrastructure
   ```

2. **Redis**:
   Ensure Redis is running (via Aspire or local Docker).

3. **Gemini API Key**:
   Set `Google:Gemini:ApiKey` in your environment variables or user secrets.

4. **Intent Classification Model**:
   Configure `IntentClassification:ModelName` to `gemini-2.5-flash-lite` in `appsettings.Development.json`.

## Testing the Feature

### 1. Create Core Instruction
```bash
curl -X POST http://localhost:8080/chatbot/v1/admin/instructions \
  -H "Content-Type: application/json" \
  -d 
'{ 
    "name": "Maliev Persona",
    "category": "Core",
    "personaDefinition": "You are a friendly expert for Maliev Manufacturing.",
    "businessConstraints": "Only talk about manufacturing.",
    "isActive": true
  }'
```

### 2. Create Topic Instruction
```bash
curl -X POST http://localhost:8080/chatbot/v1/admin/instructions \
  -H "Content-Type: application/json" \
  -d 
'{ 
    "name": "3D Scanning Domain",
    "category": "Topic",
    "topicKey": "3D-Scanning",
    "personaDefinition": "You have expert knowledge in 3D scanning.",
    "businessConstraints": "Explain our on-site and in-house scanning services.",
    "isActive": true
  }'
```

### 3. Test Intent Detection
Send a message: "Can you tell me about your 3D scanning services?"
Observe `MetadataJson` in the response or database.
```json
{
  "intent": "3D-Scanning",
  "confidence": 0.98,
  "injectedTopicKeys": ["3D-Scanning"]
}
```
