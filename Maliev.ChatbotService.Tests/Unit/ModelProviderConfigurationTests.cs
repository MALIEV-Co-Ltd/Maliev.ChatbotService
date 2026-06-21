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
