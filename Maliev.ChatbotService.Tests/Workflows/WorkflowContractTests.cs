namespace Maliev.ChatbotService.Tests.Workflows;

public sealed class WorkflowContractTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Workflows = Path.Combine(Root, ".github", "workflows");

    [Fact]
    public void PullRequests_AlwaysUseReadOnlyReusableValidation()
    {
        var source = Read("pr-validation.yml");

        Assert.Contains("pull_request:", source, StringComparison.Ordinal);
        Assert.Contains("contents: read", source, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/_validate.yml", source, StringComparison.Ordinal);
        Assert.DoesNotContain("paths:", source, StringComparison.Ordinal);
        AssertSafe(source);
    }

    [Theory]
    [InlineData("ci-main.yml", "main")]
    [InlineData("ci-develop.yml", "develop")]
    [InlineData("ci-staging.yml", "release/v*")]
    public void BranchAndTagWorkflows_AreValidationOnly(string file, string trigger)
    {
        var source = Read(file);

        Assert.Contains(trigger, source, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/_validate.yml", source, StringComparison.Ordinal);
        AssertSafe(source);
    }

    [Fact]
    public void ReusableValidation_IsCredentialFreeAndUsesImmutablePublicSources()
    {
        var source = Read("_validate.yml");

        Assert.Contains("workflow_call:", source, StringComparison.Ordinal);
        Assert.Contains("name: validate", source, StringComparison.Ordinal);
        Assert.Contains("actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1", source, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68", source, StringComparison.Ordinal);
        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.Aspire", source, StringComparison.Ordinal);
        Assert.Contains("repository: MALIEV-Co-Ltd/Maliev.MessagingContracts", source, StringComparison.Ordinal);
        Assert.Equal(3, source.Split("/p:GITHUB_ACTIONS=false", StringSplitOptions.None).Length - 1);
        AssertSafe(source);
    }

    [Fact]
    public void ProjectGraph_CanUsePublicSharedSourceWithoutPrivatePackages()
    {
        foreach (var file in new[]
        {
            "Maliev.ChatbotService.Api/Maliev.ChatbotService.Api.csproj",
            "Maliev.ChatbotService.Application/Maliev.ChatbotService.Application.csproj",
            "Maliev.ChatbotService.Infrastructure/Maliev.ChatbotService.Infrastructure.csproj",
        })
        {
            var source = File.ReadAllText(Path.Combine(Root, file));
            Assert.Contains("$(SharedSourceRoot)", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryWorkflow_ForbidsCredentialsAndDeploymentMutation()
    {
        foreach (var file in Directory.GetFiles(Workflows, "*.yml"))
        {
            AssertSafe(File.ReadAllText(file));
        }
    }

    private static void AssertSafe(string source)
    {
        foreach (var forbidden in new[]
        {
            "secrets.", "GITOPS_PAT", "GCP_SA_KEY", "NUGET_PASSWORD", "id-token: write",
            "credentials_json", "google-github-actions/auth", "gcloud auth", "docker push",
            "maliev-gitops", "kustomize edit", "gh pr create", "pull_request_target",
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Read(string file)
    {
        var path = Path.Combine(Workflows, file);
        Assert.True(File.Exists(path), $"Required workflow is missing: {file}");
        return File.ReadAllText(path);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.ChatbotService.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ChatbotService repository root.");
    }
}
