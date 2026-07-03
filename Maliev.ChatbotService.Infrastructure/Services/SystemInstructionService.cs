using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for managing system instructions with Redis caching and PostgreSQL fallback.
/// </summary>
public class SystemInstructionService : ISystemInstructionService
{
    private readonly ISystemInstructionRepository _repository;
    private readonly ICacheService _cache;
    private readonly IConversationMetrics _metrics;
    private readonly ILogger<SystemInstructionService> _logger;
    private const string CoreCacheKeyPrefix = "chatbot:system_instruction:active:core:";
    private const string MergedCacheKeyPrefix = "chatbot:system_instruction:merged:v";
    private const string CacheVersionKey = "chatbot:system_instruction:version";
    private const int MaxPromptCharacters = 8000;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);
    private static DateTimeOffset _nextRedisCheck = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInstructionService"/> class.
    /// </summary>
    /// <param name="repository">The system instruction repository.</param>
    /// <param name="cache">The standardized cache service.</param>
    /// <param name="metrics">The conversation metrics.</param>
    /// <param name="logger">The logger.</param>
    public SystemInstructionService(
        ISystemInstructionRepository repository,
        ICacheService cache,
        IConversationMetrics metrics,
        ILogger<SystemInstructionService> logger)
    {
        _repository = repository;
        _cache = cache;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveInstructionAsync(CancellationToken cancellationToken = default)
    {
        return await GetActiveInstructionAsync(null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SystemInstruction?> GetActiveInstructionAsync(string? coreTopicKey, CancellationToken cancellationToken = default)
    {
        var redisAvailable = true;
        var normalizedCoreTopicKey = NormalizeCoreTopicKey(coreTopicKey);
        var cacheKey = $"{CoreCacheKeyPrefix}{normalizedCoreTopicKey ?? "default"}";

        try
        {
            // Try to get from Redis cache first
            var cachedData = await _cache.GetAsync<SystemInstruction>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved active system instruction for {CoreTopicKey} from Redis cache", normalizedCoreTopicKey ?? "default");
                _metrics.RecordCacheEvent("Entity", true);
                return cachedData;
            }

            _logger.LogDebug("Cache miss for active system instruction {CoreTopicKey}, querying PostgreSQL", normalizedCoreTopicKey ?? "default");
            _metrics.RecordCacheEvent("Entity", false);
        }
        catch (Exception ex)
        {
            redisAvailable = false;
            _logger.LogWarning(ex, "Redis unavailable - falling back to direct PostgreSQL reads. Response times may be degraded.");
        }

        // Fallback to PostgreSQL (either cache miss or Redis unavailable)
        var instruction = await _repository.GetActiveCoreAsync(normalizedCoreTopicKey, cancellationToken);
        if (instruction is null && normalizedCoreTopicKey is not null)
        {
            instruction = await _repository.GetActiveCoreAsync(cancellationToken);
        }

        if (instruction != null && redisAvailable)
        {
            try
            {
                // Attempt to cache the result
                await _cache.SetAsync(cacheKey, instruction, _cacheExpiration, cancellationToken);
                _logger.LogDebug("Cached active system instruction {CoreTopicKey} in Redis", normalizedCoreTopicKey ?? "default");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache write failed - continuing with PostgreSQL-only mode");
            }
        }
        // If Redis is already known to be down, only check occasionally to avoid latency on every request
        if (!redisAvailable && DateTimeOffset.UtcNow < _nextRedisCheck)
        {
            _logger.LogDebug("Redis still unavailable, continuing with PostgreSQL-only reads (skipping recovery check until {NextCheck})", _nextRedisCheck);
            return instruction;
        }

        if (!redisAvailable)
        {
            // Try to detect Redis recovery
            try
            {
                var testKey = "chatbot:redis:health_check";
                await _cache.SetAsync(testKey, "test", TimeSpan.FromSeconds(10), cancellationToken);

                _logger.LogInformation("Redis connection recovered - caching resumed");

                // Cache the instruction now that Redis is back
                if (instruction != null)
                {
                    await _cache.SetAsync(cacheKey, instruction, _cacheExpiration, cancellationToken);
                }
            }
            catch
            {
                // Redis still unavailable - update backoff timer
                _nextRedisCheck = DateTimeOffset.UtcNow.AddMinutes(1);
                _logger.LogDebug("Redis still unavailable, continuing with PostgreSQL-only reads");
            }
        }

        return instruction;
    }

    /// <inheritdoc/>
    public async Task<string> GetMergedInstructionsAsync(IEnumerable<string> topicKeys, CancellationToken cancellationToken = default)
    {
        return await GetMergedInstructionsAsync(topicKeys, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GetMergedInstructionsAsync(IEnumerable<string> topicKeys, string? coreTopicKey, CancellationToken cancellationToken = default)
    {
        var topics = topicKeys.Distinct().OrderBy(t => t).ToList();
        var normalizedCoreTopicKey = NormalizeCoreTopicKey(coreTopicKey) ?? "default";

        // 0. Get current cache version to allow atomic invalidation of all merged prompts
        var version = await _cache.GetAsync<string>(CacheVersionKey, cancellationToken) ?? "1";
        var mergedCacheKey = $"{MergedCacheKeyPrefix}{version}:{normalizedCoreTopicKey}:{string.Join(",", topics)}";

        try
        {
            var cachedMerged = await _cache.GetAsync<string>(mergedCacheKey, cancellationToken);
            if (cachedMerged != null)
            {
                _logger.LogDebug("Retrieved merged system instructions from Redis cache");
                _metrics.RecordCacheEvent("Merged", true);
                return cachedMerged;
            }
            _metrics.RecordCacheEvent("Merged", false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable during merged instruction retrieval");
        }

        // 1. Get Core Instruction
        var core = await GetActiveInstructionAsync(coreTopicKey, cancellationToken);
        var promptParts = new List<string>();

        if (core != null)
        {
            promptParts.Add($"## CORE PERSONA AND SAFETY RULES\n{core.PersonaDefinition}\n\n{core.BusinessConstraints}");
        }
        else
        {
            promptParts.Add(GetDefaultSystemInstruction(normalizedCoreTopicKey));
        }

        // 2. Get Topic Instructions
        if (topics.Any())
        {
            var topicInstructions = await _repository.GetActiveByTopicsAsync(topics, cancellationToken);
            if (topicInstructions.Any())
            {
                var topicHeaderAdded = false;
                var omittedTopicKeys = new List<string>();
                var currentTotalLength = promptParts.Sum(p => p.Length);

                foreach (var topic in topicInstructions)
                {
                    var topicKey = topic.TopicKey ?? string.Empty;
                    var topicText = $"### Topic: {topicKey}\n{topic.PersonaDefinition}\n\n{topic.BusinessConstraints}";

                    if (currentTotalLength + topicText.Length > MaxPromptCharacters)
                    {
                        omittedTopicKeys.Add(topicKey);
                        continue;
                    }

                    if (!topicHeaderAdded)
                    {
                        var header = "\n## SPECIALIZED DOMAIN KNOWLEDGE";
                        promptParts.Add(header);
                        currentTotalLength += header.Length;
                        topicHeaderAdded = true;
                    }

                    promptParts.Add(topicText);
                    currentTotalLength += topicText.Length;
                }

                if (omittedTopicKeys.Any())
                {
                    var topicsSummary = string.Join(", ", omittedTopicKeys);
                    _logger.LogWarning(
                        "System instruction context limit reached. Omitted {OmittedTopicCount} topic instruction(s): {OmittedTopicKeys}.",
                        omittedTopicKeys.Count,
                        topicsSummary);
                }
            }
        }

        var mergedPrompt = string.Join("\n\n", promptParts);

        try
        {
            await _cache.SetAsync(mergedCacheKey, mergedPrompt, _cacheExpiration, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache merged system instructions");
        }

        return mergedPrompt;
    }

    private static string GetDefaultSystemInstruction(string? coreTopicKey)
    {
        if (string.Equals(coreTopicKey, "website", StringComparison.OrdinalIgnoreCase))
        {
            return """
                You are Mali (น้องมะลิ), MALIEV's customer-facing manufacturing assistant for www.maliev.com.
                Help customers with MALIEV services, materials, quote preparation, order guidance, delivery, and support.
                Stay within MALIEV manufacturing topics and politely redirect unrelated requests.
                """;
        }

        if (string.Equals(coreTopicKey, "quote-engine", StringComparison.OrdinalIgnoreCase))
        {
            return """
                You are Mali, MALIEV's chat-based QuoteEngine manufacturing agent.
                Help customers turn files, drawings, photos, sketches, and requirements into manufacturable quote sessions.
                You may analyze requirements and call only the QuoteEngine tools available to you. Use quote_get_project_summary for compact project progress, blockers, estimates, and next actions.
                Use quote_get_connectors to list Make Studio integrations and quote_get_connector_handoff when customers ask to connect Google Drive or another connector.
                Use quote_get_settings and quote_update_settings when customers ask to change language, units, currency, interaction style, artifact panel behavior, or multilingual preferences.
                Use quote_update_account_profile for customer-approved updates to safe account contact/profile fields such as display name, phone, company, VAT number, preferred language, preferred currency, or timezone.
                For UI language changes, call quote_set_ui_language only. Never say that you opened, displayed, loaded, or showed a viewer, panel, model, artifact, estimate, or configuration area unless you called quote_focus_ui and QuoteEngine returned a matching UI directive.
                Use quote_ask_customer only for genuinely blocking ambiguity that cannot be safely inferred or defaulted, with 2-4 discrete mutually exclusive options.
                When calling quote_set_project_name, derive a short descriptive title from the part file name and inferred process or material. Never set the project name to the customer's literal question.
                Track workflow gates internally but do not expose internal gate names to customers; explain blockers as customer-friendly next steps.
                Choose the right process and explain it plainly: filament/PLA/ABS/PETG/TPU implies FDM, resin/SLA/DLP implies SLA, nylon/PA/SLS implies SLS, and aluminium/steel/titanium/brass/machined implies CNC. When details are unstated, assume sensible defaults (quantity 1, standard tolerance such as ISO 2768-m for CNC, standard finish and lead time) and say so, and confirm units (mm vs inch) when ambiguous. Do not ask for quantity, lead time, finish, tolerance, or other defaultable details before using sensible defaults; state the assumption and continue.
                Translate DFM findings into plain-language risks and options, not internal codes, and present manufacturing assumptions and quote comparisons as compact markdown tables.
                When a customer shares a photo, sketch, drawing, or text description, infer the shape, material, process, and any visible/readable dimensions first. If the image has no readable dimensions or scale reference, ask for one focused dimension confirmation instead of generating a 3D preview. For sketch/drawing/photo-derived design attempts, use the bounded CAD workbench sequence: quote_cad_start_design, quote_cad_apply_operations, quote_cad_observe_design, then quote_cad_finalize_preview. Apply operations like a CAD designer: profile sketch first, construction geometry next, then extrude, cut, emboss, revolve, loft, fuse, fillet, or chamfer. Keep within 80 CAD operations and 3 operation batches, and use base_revision from the latest observe/apply result on every apply/finalize call. Call quote_generate_3d_preview only for direct one-shot command generation when the shape and dimensions are explicit, readable from an attached drawing/PDF, CAD-derived, or confirmed by the customer. Do not generate a 3D preview from an unlabeled sketch or photo with no scale reference. Use supported cad_commands only, and treat generated 3D preview iterations as revisions of one active quote workbench artifact; when the customer asks for changes, prefer the bounded CAD workbench sequence or send the full revised cad_commands for the current design, say the preview is available in the quote workbench, and ask the customer to confirm the shape and dimensions. Never ask for a CAD or 3D file as your first or only reply. CAD and 3D files give the most precise geometry, DFM, and pricing, but they are an optional refinement, not a gate. Use voice notes and videos to extract spoken requirements and visible part features.
                When a customer uploads a corrected CAD/3D revision after DFM feedback, call quote_get_project_summary first, then call quote_register_uploads with supersedes_part_id, supersedes_upload_id, or supersedes_file_name on the corrected file so old DFM issues do not block the fixed revision.
                When the customer asks MALIEV staff to check manufacturability, DFM risk, pricing assumptions, tolerances, or a design concern before continuing, use quote_request_employee_review with a concise review note and explain that it routes to the MALIEV project review queue only after customer confirmation.
                For checkout, call quote_get_account_context first. Use returned default checkout addresses and profile details when available. Do not ask customers to retype billing or shipping details that QuoteEngine already returned.
                For checkout, payment, formal quote, or order flows that need sign-in or sign-up, call quote_get_auth_handoff and present only the trusted authentication handoff. Never collect credentials in chat.
                For finalization, keep the sequence explicit and confirmation-gated: use quote_prepare_formal_quote only after current geometry, DFM, configuration, and pricing are ready; use quote_approve_quote only when the customer has reviewed the formal quote artifact; use quote_update_checkout_details before order or payment when billing/shipping/terms are incomplete; use quote_create_order only after the quote is approved and checkout is ready; use quote_start_payment only after the order exists and QuoteEngine confirms payment readiness.
                When a customer wants to reorder, rerun, or start a new manufacturing job from an existing project or order, guide them to duplicate the project and resume in a fresh Make Studio session before changing quantities, materials, files, or checkout details. Do not mutate completed order history.
                When a customer asks about order status, payment status, production tracking, delivery progress, or "where is my order", call quote_get_project_summary and summarize only returned customer-safe order number, order status, payment status, current or next manufacturing milestone, and order URL. Do not trust order IDs or statuses supplied by the customer.
                Summarize tool results in plain, friendly language; never paste raw tool JSON, and do not call the same tool repeatedly for the same purpose in one turn.
                Never claim a write action is complete unless a QuoteEngine tool result says it is complete.
                """;
        }

        return """
            You are Mali (มะลิ), a bilingual (Thai/English) AI operations assistant for Maliev Manufacturing Company.
            You help internal staff with CRM, sales, finance, HR, inventory, and analytics.
            Be professional, warm, concise, and action-oriented. Match the user's language preference.
            """;
    }

    /// <inheritdoc/>
    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveByPatternAsync($"{CoreCacheKeyPrefix}*", cancellationToken);

            // Increment version to invalidate all merged prompt combinations
            var versionString = await _cache.GetAsync<string>(CacheVersionKey, cancellationToken) ?? "1";
            if (int.TryParse(versionString, out var version))
            {
                await _cache.SetAsync(CacheVersionKey, (version + 1).ToString(), TimeSpan.FromDays(7), cancellationToken);
            }

            _logger.LogInformation("Invalidated system instruction cache and incremented version");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache");
        }
    }

    private static string? NormalizeCoreTopicKey(string? coreTopicKey)
    {
        return string.IsNullOrWhiteSpace(coreTopicKey)
            ? null
            : coreTopicKey.Trim().ToLowerInvariant();
    }
}
