# Implementation Summary: Phases 9-11

## Overview

This document summarizes the implementation of Phases 9-11 for the Unified Chatbot Service, covering:
- **Phase 9**: Graceful Degradation (T218-T224)
- **Phase 10**: Controlled Web Search (T225-T231)
- **Phase 11**: Webhook Integration (T232-T243)

---

## Phase 9: Graceful Degradation ✅ COMPLETED

### Files Modified

#### 1. `Maliev.ChatbotService.Infrastructure/AI/GeminiClient.cs`

**Changes:**
- Added exponential backoff retry logic with 3 attempts (1s, 2s, 4s delays)
- Added fallback response generation for multiple error scenarios
- Implemented transient error detection (503, 429, 500 status codes)
- Added dedicated timeout handling (no retry on timeout)
- Created `GetFallbackResponse()` method with predefined user-friendly messages

**Key Features:**
```csharp
private const int MaxRetries = 3;
private static readonly int[] RetryDelaysMs = { 1000, 2000, 4000 };

// Fallback messages for:
- GeminiAPITimeout
- GeminiAPIError
- RedisUnavailable
- ValidationFailure
- UnexpectedError
```

#### 2. `Maliev.ChatbotService.Application/Interfaces/IGeminiClient.cs`

**Changes:**
- Added `IsFallback` property to `GeminiResponse` class to indicate fallback responses

#### 3. `Maliev.ChatbotService.Infrastructure/Services/SystemInstructionService.cs`

**Changes:**
- Enhanced Redis failure detection and logging
- Added automatic Redis recovery detection
- Improved fallback to PostgreSQL with performance degradation logging
- Health check mechanism to detect when Redis comes back online

**Key Features:**
```csharp
// Redis unavailable → immediate fallback to PostgreSQL
// Logs: "Redis unavailable - falling back to direct PostgreSQL reads"
// On next call: attempts health check to detect recovery
// Logs: "Redis connection recovered - caching resumed"
```

---

## Phase 10: Controlled Web Search ⚠️ PARTIALLY IMPLEMENTED

### Files Modified

#### 1. `Maliev.ChatbotService.Domain/Entities/SystemInstruction.cs` ✅

**Changes:**
- Added `EnableWebSearch` property (bool) - controls whether web search is allowed
- Added `LogSearchDomains` property (bool, default=true) - controls domain logging

### Files Requiring Manual Updates

#### 2. `Maliev.ChatbotService.Application/Handlers/SendMessageCommandHandler.cs` ⚠️ REQUIRES UPDATE

**Required Changes:**

##### A. Add Dependencies to Constructor
```csharp
private readonly ISearchDomainLogRepository _searchDomainLogRepository;
private readonly IWebSearchService _webSearchService;

// Add to constructor parameters:
ISearchDomainLogRepository searchDomainLogRepository,
IWebSearchService webSearchService,

// Add to constructor body:
_searchDomainLogRepository = searchDomainLogRepository;
_webSearchService = webSearchService;
```

##### B. Add Web Search Trigger Detection Method
```csharp
/// <summary>
/// Detects whether the user message requires web search based on keywords.
/// </summary>
/// <param name="message">The user message.</param>
/// <returns>True if web search should be triggered.</returns>
private static bool ShouldTriggerWebSearch(string message)
{
    var searchKeywords = new[]
    {
        "astm", "iso", "din", "jis", "spec", "specification",
        "standard", "properties", "technical", "datasheet",
        "material properties", "chemical composition"
    };

    var messageLower = message.ToLowerInvariant();
    return searchKeywords.Any(keyword => messageLower.Contains(keyword));
}
```

##### C. Add Web Search Logic to HandleAsync Method

Insert AFTER getting system instruction (around line 210):

```csharp
// Check if web search should be triggered
string? webSearchContext = null;
if (systemInstruction != null &&
    systemInstruction.EnableWebSearch &&
    ShouldTriggerWebSearch(command.Content))
{
    _logger.LogInformation("Web search triggered for query: {Query}", command.Content);

    try
    {
        // Perform web search with 30s timeout
        using var searchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        searchCts.CancelAfter(TimeSpan.FromSeconds(30));

        var searchResults = await _webSearchService.SearchAsync(command.Content, searchCts.Token);

        // Log domains if enabled
        if (systemInstruction.LogSearchDomains && searchResults.Count > 0)
        {
            var domains = searchResults.Select(r => r.Domain).Distinct().ToList();

            foreach (var domain in domains)
            {
                await _searchDomainLogRepository.CreateAsync(new SearchDomainLog
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    Domain = domain,
                    SearchQuery = command.Content,
                    AccessedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
            }

            _logger.LogInformation("Logged {Count} domains for web search query", domains.Count);
        }

        // Build web search context for Gemini
        if (searchResults.Count > 0)
        {
            webSearchContext = "Web search results:\n" +
                string.Join("\n", searchResults.Take(5).Select((r, i) =>
                    $"{i + 1}. {r.Title}\n   {r.Snippet}\n   Source: {r.Url}"));
        }
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Web search timed out after 30 seconds for query: {Query}", command.Content);
        // Continue without web search results
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error performing web search for query: {Query}", command.Content);
        // Continue without web search results
    }
}
```

##### D. Update Gemini Request Building

REPLACE the existing Gemini request building (around line 270):

```csharp
// Build Gemini request with web search context if available
var enhancedSystemInstruction = systemInstructionText;
if (!string.IsNullOrEmpty(webSearchContext))
{
    enhancedSystemInstruction += $"\n\n{webSearchContext}\n\nPlease use the web search results above to provide accurate, sourced information.";
}

var geminiRequest = new GeminiRequest
{
    SystemInstruction = enhancedSystemInstruction,
    Messages = conversationHistory
        .OrderBy(m => m.CreatedAt)
        .Select(m => new GeminiMessage
        {
            Role = m.Role == MessageRole.User ? "user" : "assistant",
            Content = m.Content
        })
        .ToList(),
    TimeoutSeconds = !string.IsNullOrEmpty(webSearchContext) ? 30 : 10 // Extended timeout for web search
};
```

##### E. Add Business Rule Enforcement

Add BEFORE web search execution:

```csharp
// Enforce business rules: prevent competitor pricing queries
var competitorKeywords = new[] { "competitor", "pricing", "cost comparison", "price comparison" };
var isCompetitorQuery = competitorKeywords.Any(k => command.Content.ToLowerInvariant().Contains(k));

if (isCompetitorQuery)
{
    _logger.LogWarning("Blocked competitor pricing query: {Query}", command.Content);
    // Don't perform web search for competitor queries
    continue; // Skip web search
}
```

---

## Phase 11: Webhook Integration ✅ COMPLETED

### Files Already Implemented

#### 1. `Maliev.ChatbotService.Api/Controllers/V1/WebhooksController.cs` ✅

**Features Implemented:**
- ✅ LINE webhook handler (`POST /v1/webhooks/line`)
- ✅ Facebook/Instagram/WhatsApp webhook handler (`POST /v1/webhooks/meta`)
- ✅ Meta verification challenge handler (`GET /v1/webhooks/meta`)
- ✅ Signature validation for LINE (X-Line-Signature header)
- ✅ Signature validation for Meta platforms (X-Hub-Signature-256 header)
- ✅ Channel-specific message processing
- ✅ Error handling for individual events (continues processing on error)

**Endpoints:**
```
POST /chatbot/v1/webhooks/line
POST /chatbot/v1/webhooks/meta
GET /chatbot/v1/webhooks/meta (verification)
```

---

## Database Migration Required

### Add SystemInstruction Fields

Create migration:
```bash
cd Maliev.ChatbotService.Infrastructure
dotnet ef migrations add AddWebSearchFieldsToSystemInstruction
```

Expected migration content:
```csharp
migrationBuilder.AddColumn<bool>(
    name: "EnableWebSearch",
    table: "SystemInstructions",
    type: "boolean",
    nullable: false,
    defaultValue: false);

migrationBuilder.AddColumn<bool>(
    name: "LogSearchDomains",
    table: "SystemInstructions",
    type: "boolean",
    nullable: false,
    defaultValue: true);
```

---

## Testing Recommendations

### Phase 9: Graceful Degradation
1. Test Gemini API timeout (simulate with 1s timeout)
2. Test Gemini API 503 error (retry behavior)
3. Test Redis unavailability (stop Redis, verify PostgreSQL fallback)
4. Test Redis recovery (start Redis again, verify caching resumes)

### Phase 10: Web Search
1. Test web search trigger with "ASTM A36 properties"
2. Test domain logging to SearchDomainLog table
3. Test web search disabled (EnableWebSearch=false)
4. Test competitor query blocking
5. Test 30s timeout for slow search

### Phase 11: Webhooks
1. Test LINE webhook with valid signature
2. Test Meta webhook with valid signature
3. Test signature validation rejection
4. Test multi-platform message processing

---

## Configuration Required

### appsettings.json

```json
{
  "GoogleCustomSearch": {
    "ApiKey": "YOUR_GOOGLE_API_KEY",
    "SearchEngineId": "YOUR_SEARCH_ENGINE_ID"
  },
  "Meta": {
    "VerifyToken": "YOUR_META_VERIFY_TOKEN",
    "AppSecret": "YOUR_META_APP_SECRET"
  },
  "Line": {
    "ChannelSecret": "YOUR_LINE_CHANNEL_SECRET"
  }
}
```

---

## Summary

### Completed ✅
- Phase 9: Graceful Degradation (GeminiClient + SystemInstructionService)
- Phase 11: Webhook Integration (WebhooksController)
- SystemInstruction entity updated with web search fields

### Requires Manual Updates ⚠️
- SendMessageCommandHandler: Add web search integration (see section above)
- Database migration: Add EnableWebSearch and LogSearchDomains columns
- Configuration: Add Google Custom Search API keys

### Estimated Time to Complete
- SendMessageCommandHandler updates: 30 minutes
- Database migration: 5 minutes
- Testing: 1-2 hours
- **Total: ~2-3 hours**

---

## Next Steps

1. Update `SendMessageCommandHandler.cs` with web search logic (see Phase 10 section above)
2. Create and apply database migration
3. Configure Google Custom Search API credentials
4. Run integration tests to verify all three phases
5. Update system instructions in database to enable/disable web search as needed
