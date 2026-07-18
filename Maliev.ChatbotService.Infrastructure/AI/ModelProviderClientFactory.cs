using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Creates model provider clients by provider key.
/// </summary>
public sealed class ModelProviderClientFactory : IModelProviderClientFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelProviderClientFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public ModelProviderClientFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public IModelProviderClient Create(string? providerName)
    {
        return NormalizeProviderName(providerName) switch
        {
            "openai-compatible" => _serviceProvider.GetRequiredService<OpenAICompatibleModelProviderClient>(),
            "gemini" => _serviceProvider.GetRequiredService<GeminiModelProviderClient>(),
            var unsupported => throw new InvalidOperationException(
                $"Unsupported LLM provider '{unsupported}'. Supported providers: gemini, openai-compatible.")
        };
    }

    private static string NormalizeProviderName(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return "gemini";
        }

        return providerName.Trim().ToLowerInvariant() switch
        {
            "google" => "gemini",
            "google-gemini" => "gemini",
            "openai" => "openai-compatible",
            "deepseek" => "openai-compatible",
            "qwen" => "openai-compatible",
            "kimi" => "openai-compatible",
            "moonshot" => "openai-compatible",
            "openrouter" => "openai-compatible",
            "groq" => "openai-compatible",
            var normalized => normalized
        };
    }
}
