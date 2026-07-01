using Maliev.ChatbotService.Application.Configuration;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class GeminiUtilityRequestOptionsTests
{
    [Theory]
    [InlineData(" FLEX ", "flex", GeminiRequest.FlexInferenceTimeoutSeconds)]
    [InlineData("Priority", "priority", 5)]
    [InlineData(" STANDARD ", "standard", 5)]
    public void FromConfiguration_WhenServiceTierConfigured_NormalizesDocumentedEnum(
        string configuredServiceTier,
        string expectedServiceTier,
        int expectedTimeoutSeconds)
    {
        var configuration = CreateConfiguration(configuredServiceTier);

        var options = GeminiUtilityRequestOptions.FromConfiguration(configuration);

        Assert.Equal(expectedServiceTier, options.ServiceTier);
        Assert.Equal(expectedTimeoutSeconds, options.TimeoutSeconds);
    }

    [Fact]
    public void FromConfiguration_WhenServiceTierUnsupported_ThrowsConfigurationError()
    {
        var configuration = CreateConfiguration("discount");

        var exception = Assert.Throws<InvalidOperationException>(
            () => GeminiUtilityRequestOptions.FromConfiguration(configuration));

        Assert.Contains("Gemini:UtilityRequests:ServiceTier", exception.Message);
        Assert.Contains("standard", exception.Message);
        Assert.Contains("flex", exception.Message);
        Assert.Contains("priority", exception.Message);
    }

    private static IConfiguration CreateConfiguration(string serviceTier)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:UtilityRequests:ServiceTier"] = serviceTier
            })
            .Build();
    }
}
