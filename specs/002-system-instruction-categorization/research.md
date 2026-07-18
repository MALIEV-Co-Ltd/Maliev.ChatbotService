# Research: Categorized System Instructions & Dynamic Injection

## Decision: Intent Classification Strategy
- **Method**: Use a small, fast model (`gemini-2.5-flash-lite` or `gemini-1.5-flash`) to classify the user's message into one or more `TopicKey`s.
- **Prompting**: A structured classification prompt that returns JSON containing `intent` and `confidence`.
- **Rationale**: Keeps the main prompt focused and token-efficient.

## Decision: Caching Strategy
- **Layer 1 (Entity Cache)**: Cache individual `SystemInstruction` and `KnowledgeBase` entries in Redis by `Id` and `TopicKey`.
- **Layer 2 (Merged Cache)**: Cache the merged system prompt for a combination of `(CoreId, [TopicIds])` to avoid redundant string concatenation.
- **Layer 3 (Session Intent)**: Store the "Active Topic" in the `ConversationSession` entity to avoid re-classifying every single message if the topic hasn't changed.
- **Rationale**: Minimizes latency and database load.

## Decision: Knowledge Base Retrieval
- **Method**: Direct SQL lookup via EF Core for `TopicKey` + `FactKey` (if specified) or just `TopicKey` (all facts for that topic).
- **Rationale**: The current requirement is for specific facts like "Pricing". Structured SQL/JSONB lookup is faster and more precise than vector search for structured business data.

## Decision: Instruction Merging Rules
1. **Core Priority**: Core instructions are always first.
2. **Precedence**: Core instructions use wording like "UNDER NO CIRCUMSTANCES should you..." for safety. Topic instructions provide factual overrides for domains.
3. **Truncation**: If token limits are approached, Topic instructions are truncated based on `Priority`, then `Intent Confidence`. Core is never truncated.
- **Rationale**: Ensures safety and persona consistency while allowing specialized knowledge.

## Alternatives Considered
- **Vector Search (RAG)**: Overkill for the current requirement of "Topic-specific instructions". Most instructions are rule-based, not just text blobs.
- **Single Prompt with all Instructions**: Leads to "lost in the middle" phenomena and high token costs. Categorization solves this.

## Unresolved (To be handled during implementation)
- Exact token threshold for "Safe Injection": Will start with 8k tokens and tune.
