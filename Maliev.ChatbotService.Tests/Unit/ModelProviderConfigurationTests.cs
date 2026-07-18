using System.Text.Json;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class ModelProviderConfigurationTests
{
    [Fact]
    public void Program_RegistersProviderRouterInsteadOfDirectGeminiClient()
    {
        var program = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "Program.cs"));

        Assert.Contains("IModelProviderClient", program, StringComparison.Ordinal);
        Assert.Contains("ProviderRoutingGeminiClient", program, StringComparison.Ordinal);
        Assert.Contains("GeminiModelProviderClient", program, StringComparison.Ordinal);
        Assert.Contains("OpenAICompatibleModelProviderClient", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHttpClient<IGeminiClient, GeminiClient>", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_ModelProviderHttpClients_DoNotRetryProviderRateLimits()
    {
        var program = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "Program.cs"));

        var geminiRegistration = ExtractSourceBlock(
            program,
            "AddHttpClient<GeminiModelProviderClient>",
            "AddHttpClient<OpenAICompatibleModelProviderClient>");
        var openAiCompatibleRegistration = ExtractSourceBlock(
            program,
            "AddHttpClient<OpenAICompatibleModelProviderClient>",
            "builder.Services.AddHttpClient<IWebSearchService");
        var retryPolicy = ExtractSourceBlock(
            program,
            "static ValueTask<bool> ShouldRetryModelProviderFailure",
            "internal static partial class Log");

        Assert.Contains("ShouldRetryModelProviderFailure(", geminiRegistration, StringComparison.Ordinal);
        Assert.Contains("ShouldRetryModelProviderFailure(", openAiCompatibleRegistration, StringComparison.Ordinal);
        Assert.Contains("HttpStatusCode.TooManyRequests", retryPolicy, StringComparison.Ordinal);
        Assert.Contains("ValueTask.FromResult(false)", retryPolicy, StringComparison.Ordinal);
        Assert.Contains("HttpStatusCode.RequestTimeout", retryPolicy, StringComparison.Ordinal);
        Assert.Contains(">= 500", retryPolicy, StringComparison.Ordinal);
        Assert.Contains("HttpRequestException", retryPolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_GeminiBackedOpenAiCompatibleProvider_KeepsNativeGeminiCostFeatureClients()
    {
        var program = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "Program.cs"));

        var featureClientGate = ExtractSourceBlock(
            program,
            "var usesGeminiApiFeatures",
            "builder.Services.AddHttpClient<GeminiModelProviderClient>");

        Assert.Contains("UsesGeminiApiFeatureProvider(", featureClientGate, StringComparison.Ordinal);
        Assert.Contains("externalClientsConfig.OpenAICompatible.BaseAddress", featureClientGate, StringComparison.Ordinal);
        Assert.Contains("IModelContextCacheService, GeminiModelContextCacheService", featureClientGate, StringComparison.Ordinal);
        Assert.Contains("IModelBatchClient, GeminiBatchClient", featureClientGate, StringComparison.Ordinal);
        Assert.Contains("IModelFileStagingService, GeminiModelFileStagingService", featureClientGate, StringComparison.Ordinal);

        var helperBlock = ExtractSourceBlock(
            program,
            "private static bool UsesGeminiApiFeatureProvider",
            "private static ValueTask<bool> ShouldRetryModelProviderFailure");

        Assert.Contains("UsesNativeGeminiProvider(providerName)", helperBlock, StringComparison.Ordinal);
        Assert.Contains("openai-compatible", helperBlock, StringComparison.Ordinal);
        Assert.Contains("generativelanguage.googleapis.com", helperBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_DefinesConfigurableLlmProviderAndOpenAiCompatibleEndpoint()
    {
        var appsettings = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "appsettings.json"));

        Assert.Contains("\"Llm\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"Provider\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"OpenAICompatible\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"BaseAddress\"", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_DefaultsUtilityGeminiRequestsToFlexInference()
    {
        var appsettings = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "appsettings.json"));

        var utilityRequestsBlock = ExtractSourceBlock(
            appsettings,
            "\"UtilityRequests\"",
            "\"ContextCache\"");

        Assert.Contains("\"ServiceTier\": \"flex\"", utilityRequestsBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_DefinesExplicitGeminiSafetySettings()
    {
        var appsettings = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "appsettings.json"));

        var safetySettingsBlock = ExtractSourceBlock(
            appsettings,
            "\"SafetySettings\"",
            "\"ContextCache\"");

        Assert.Contains("\"Enabled\": true", safetySettingsBlock, StringComparison.Ordinal);
        Assert.Contains("\"Threshold\": \"BLOCK_ONLY_HIGH\"", safetySettingsBlock, StringComparison.Ordinal);
        Assert.Contains("\"HARM_CATEGORY_HARASSMENT\"", safetySettingsBlock, StringComparison.Ordinal);
        Assert.Contains("\"HARM_CATEGORY_HATE_SPEECH\"", safetySettingsBlock, StringComparison.Ordinal);
        Assert.Contains("\"HARM_CATEGORY_SEXUALLY_EXPLICIT\"", safetySettingsBlock, StringComparison.Ordinal);
        Assert.Contains("\"HARM_CATEGORY_DANGEROUS_CONTENT\"", safetySettingsBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSettings_DefinesVisibleGeminiCostControlDefaults()
    {
        var appsettings = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "appsettings.json"));

        using var document = JsonDocument.Parse(appsettings);
        var gemini = document.RootElement.GetProperty("Gemini");

        Assert.Equal(5L * 1024 * 1024, gemini.GetProperty("FileApiInlineThresholdBytes").GetInt64());
        Assert.Equal(3, gemini.GetProperty("FlexRetryMaxAttempts").GetInt32());
        Assert.Equal(5000, gemini.GetProperty("FlexRetryBaseDelayMs").GetInt32());
        Assert.Equal(20, gemini.GetProperty("BatchSummaryMaxSessions").GetInt32());
        Assert.Equal(18 * 1024 * 1024, gemini.GetProperty("BatchSummaryMaxInlineBytes").GetInt32());
        Assert.Equal(100, gemini.GetProperty("Webhooks").GetProperty("QueueCapacity").GetInt32());

        var agent = gemini.GetProperty("Agent");
        // Agent thoughts default ON: Make Studio must show visible reasoning, and a zero thinking
        // budget makes gemini-2.5-flash leak textual tool_code instead of native function calls.
        Assert.True(agent.GetProperty("IncludeThoughts").GetBoolean());
        Assert.Equal(1024, agent.GetProperty("ThinkingBudgetTokens").GetInt32());

        var chat = gemini.GetProperty("Chat");
        Assert.Equal("medium", chat.GetProperty("ImageMediaResolution").GetString());
        Assert.Equal("medium", chat.GetProperty("PdfMediaResolution").GetString());
        Assert.Equal("low", chat.GetProperty("VideoMediaResolution").GetString());
        Assert.Equal(
            "medium",
            gemini.GetProperty("Extraction").GetProperty("MediaResolution").GetString());
    }

    [Fact]
    public void MessagesController_PostsOnlyAbsoluteThinkingCallbackUris()
    {
        var controller = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "Maliev.ChatbotService.Api",
            "Controllers",
            "V1",
            "MessagesController.cs"));

        var validationBlock = ExtractSourceBlock(
            controller,
            "private bool TryBuildSafeCallbackUri",
            "private bool IsAllowedCallbackOrigin");
        var postBlock = ExtractSourceBlock(
            controller,
            "if (!string.IsNullOrEmpty(request.CallbackUrl))",
            "command.ThinkingStepCallback = thinkingCallback;");

        Assert.True(
            validationBlock.IndexOf("UriKind.Absolute", StringComparison.Ordinal) <
            validationBlock.IndexOf("UriKind.Relative", StringComparison.Ordinal),
            "Absolute callback URLs must be validated before relative paths.");
        Assert.Contains("callbackUri = absoluteUri;", validationBlock, StringComparison.Ordinal);
        Assert.Contains("callbackUri = resolvedUri;", validationBlock, StringComparison.Ordinal);
        Assert.Contains("!callbackUri.IsAbsoluteUri", postBlock, StringComparison.Ordinal);
        Assert.Contains("PostAsJsonAsync(callbackUri", postBlock, StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Maliev.ChatbotService.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static string ExtractSourceBlock(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find source marker: {startMarker}");

        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find source marker: {endMarker}");

        return source[start..end];
    }
}
