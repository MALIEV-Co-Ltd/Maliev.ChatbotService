using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Background service that loads markdown prompt files into the SystemInstructions table at startup.
/// Scans Prompts/core/, Prompts/topics/, and Prompts/extraction/ directories,
/// parses YAML frontmatter, and upserts into the database.
/// </summary>
public class PromptFileLoaderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PromptFileLoaderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptFileLoaderService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for creating scoped services.</param>
    /// <param name="hostEnvironment">The host environment for resolving content root path.</param>
    /// <param name="logger">The logger.</param>
    public PromptFileLoaderService(
        IServiceProvider serviceProvider,
        IHostEnvironment hostEnvironment,
        ILogger<PromptFileLoaderService> logger)
    {
        _serviceProvider = serviceProvider;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let the app finish startup
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            var promptsDir = Path.Combine(_hostEnvironment.ContentRootPath, "Prompts");
            if (!Directory.Exists(promptsDir))
            {
                _logger.LogWarning("Prompts directory not found at {Path}. Skipping prompt loading.", promptsDir);
                return;
            }

            var mdFiles = Directory.GetFiles(promptsDir, "*.md", SearchOption.AllDirectories);
            if (mdFiles.Length == 0)
            {
                _logger.LogInformation("No markdown prompt files found in {Path}.", promptsDir);
                return;
            }

            _logger.LogInformation("Found {Count} markdown prompt files to process.", mdFiles.Length);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISystemInstructionRepository>();
            var instructionService = scope.ServiceProvider.GetRequiredService<ISystemInstructionService>();

            var anyChanged = false;

            foreach (var filePath in mdFiles)
            {
                try
                {
                    var changed = await ProcessPromptFileAsync(filePath, repository, stoppingToken);
                    if (changed) anyChanged = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process prompt file: {FilePath}", filePath);
                }
            }

            if (anyChanged)
            {
                await instructionService.InvalidateCacheAsync(stoppingToken);
                _logger.LogInformation("Cache invalidated after prompt file changes.");
            }

            _logger.LogInformation("Prompt file loading completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during prompt file loading.");
        }
    }

    private async Task<bool> ProcessPromptFileAsync(
        string filePath,
        ISystemInstructionRepository repository,
        CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var (frontmatter, body) = ParseFrontmatter(content);

        if (frontmatter == null)
        {
            _logger.LogWarning("No YAML frontmatter found in {FilePath}. Skipping.", filePath);
            return false;
        }

        var name = GetFrontmatterValue(frontmatter, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("No 'name' in frontmatter of {FilePath}. Skipping.", filePath);
            return false;
        }

        var categoryStr = GetFrontmatterValue(frontmatter, "category") ?? "Topic";
        var category = categoryStr.Equals("Core", StringComparison.OrdinalIgnoreCase)
            ? SystemInstructionCategory.Core
            : SystemInstructionCategory.Topic;

        var topicKey = GetFrontmatterValue(frontmatter, "topic_key");
        var priorityStr = GetFrontmatterValue(frontmatter, "priority") ?? "10";
        _ = int.TryParse(priorityStr, out var priority);

        var isActiveStr = GetFrontmatterValue(frontmatter, "is_active") ?? "true";
        var isActive = isActiveStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        var allowedTopics = GetFrontmatterValue(frontmatter, "allowed_topics") ?? string.Empty;
        var enableWebSearchStr = GetFrontmatterValue(frontmatter, "enable_web_search") ?? "false";
        var enableWebSearch = enableWebSearchStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        // Split body into PersonaDefinition and BusinessConstraints by "## Business Constraints" heading
        var (personaDefinition, businessConstraints) = SplitBody(body);

        // Look for existing instruction by name
        var (existingInstructions, _) = await repository.GetAllAsync(1, 1000, cancellationToken: ct);
        var existing = existingInstructions.FirstOrDefault(i =>
            i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            if (PromptMatchesFile(existing, category, topicKey, priority, personaDefinition, businessConstraints, allowedTopics, isActive, enableWebSearch))
            {
                _logger.LogDebug("Prompt '{Name}' unchanged. Skipping.", name);
                return false;
            }

            // Update existing instruction
            existing.Category = category;
            existing.TopicKey = topicKey;
            existing.PersonaDefinition = personaDefinition;
            existing.BusinessConstraints = businessConstraints;
            existing.AllowedTopics = allowedTopics;
            existing.EnableWebSearch = enableWebSearch;
            existing.Priority = priority;
            existing.IsActive = isActive;
            existing.Version++;

            await repository.UpdateAsync(existing, ct);
            _logger.LogInformation("Updated prompt '{Name}' to version {Version}.", name, existing.Version);
            return true;
        }

        // Create new instruction
        var instruction = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = category,
            TopicKey = topicKey,
            Priority = priority,
            PersonaDefinition = personaDefinition,
            BusinessConstraints = businessConstraints,
            AllowedTopics = allowedTopics,
            IsActive = isActive,
            Version = 1,
            EnableWebSearch = enableWebSearch
        };

        await repository.CreateAsync(instruction, ct);
        _logger.LogInformation("Created new prompt '{Name}' (Category: {Category}, TopicKey: {TopicKey}).",
            name, category, topicKey);
        return true;
    }

    private static (Dictionary<string, string>? Frontmatter, string Body) ParseFrontmatter(string content)
    {
        content = content.Replace("\r\n", "\n");

        if (!content.StartsWith("---"))
            return (null, content);

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return (null, content);

        var yamlBlock = content[3..endIndex].Trim();
        var body = content[(endIndex + 4)..].Trim();

        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in yamlBlock.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0) continue;

            var key = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim().Trim('"');
            frontmatter[key] = value;
        }

        return (frontmatter, body);
    }

    private static string? GetFrontmatterValue(Dictionary<string, string> frontmatter, string key)
    {
        return frontmatter.TryGetValue(key, out var value) ? value : null;
    }

    private static (string PersonaDefinition, string BusinessConstraints) SplitBody(string body)
    {
        var constraintHeading = "## Business Constraints";
        var index = body.IndexOf(constraintHeading, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
            return (body.Trim(), string.Empty);

        var persona = body[..index].Trim();
        var constraints = body[index..].Trim();

        return (persona, constraints);
    }

    private static bool PromptMatchesFile(
        SystemInstruction existing,
        SystemInstructionCategory category,
        string? topicKey,
        int priority,
        string personaDefinition,
        string businessConstraints,
        string allowedTopics,
        bool isActive,
        bool enableWebSearch)
    {
        return existing.Category == category
            && string.Equals(existing.TopicKey, topicKey, StringComparison.Ordinal)
            && existing.Priority == priority
            && string.Equals(existing.PersonaDefinition, personaDefinition, StringComparison.Ordinal)
            && string.Equals(existing.BusinessConstraints, businessConstraints, StringComparison.Ordinal)
            && string.Equals(existing.AllowedTopics, allowedTopics, StringComparison.Ordinal)
            && existing.IsActive == isActive
            && existing.EnableWebSearch == enableWebSearch;
    }
}
