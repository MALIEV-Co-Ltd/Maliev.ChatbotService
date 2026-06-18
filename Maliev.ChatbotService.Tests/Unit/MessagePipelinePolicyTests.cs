using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="MessagePipelinePolicy"/> — channel-scoped topic injection (P1) and
/// customer-input normalization/length guard (S3).
/// </summary>
public class MessagePipelinePolicyTests
{
    [Theory]
    [InlineData(Channel.QuoteEngine)]
    [InlineData(Channel.Website)]
    [InlineData(Channel.Line)]
    [InlineData(Channel.Facebook)]
    public void BuildInjectableTopicKeys_CustomerChannels_NeverInjectIntranetTopics(Channel channel)
    {
        var classification = new IntentClassificationResult
        {
            Intent = "finance",
            Confidence = 0.99,
            AdditionalTopics = new List<string> { "sales", "hr" }
        };

        var keys = MessagePipelinePolicy.BuildInjectableTopicKeys(channel, classification);

        Assert.Empty(keys);
    }

    [Fact]
    public void BuildInjectableTopicKeys_Intranet_InjectsHighConfidenceIntentAndAdditionalTopics()
    {
        var classification = new IntentClassificationResult
        {
            Intent = "finance",
            Confidence = 0.99,
            AdditionalTopics = new List<string> { "sales" }
        };

        var keys = MessagePipelinePolicy.BuildInjectableTopicKeys(Channel.Intranet, classification);

        Assert.Contains("finance", keys);
        Assert.Contains("sales", keys);
    }

    [Fact]
    public void BuildInjectableTopicKeys_Intranet_GeneralOrLowConfidence_NotInjectedAsPrimary()
    {
        var general = new IntentClassificationResult { Intent = "General", Confidence = 0.99 };
        Assert.Empty(MessagePipelinePolicy.BuildInjectableTopicKeys(Channel.Intranet, general));

        var lowConfidence = new IntentClassificationResult { Intent = "finance", Confidence = 0.5 };
        Assert.DoesNotContain("finance", MessagePipelinePolicy.BuildInjectableTopicKeys(Channel.Intranet, lowConfidence));
    }

    [Fact]
    public void AllowsDomainTopicInjection_OnlyTrueForIntranet()
    {
        Assert.True(MessagePipelinePolicy.AllowsDomainTopicInjection(Channel.Intranet));
        Assert.False(MessagePipelinePolicy.AllowsDomainTopicInjection(Channel.QuoteEngine));
        Assert.False(MessagePipelinePolicy.AllowsDomainTopicInjection(Channel.Website));
    }

    [Fact]
    public void TryNormalizeContent_StripsNullBytes_AndAllowsNormalLength()
    {
        var ok = MessagePipelinePolicy.TryNormalizeContent("create a quote\0", out var normalized, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("create a quote", normalized);
    }

    [Fact]
    public void TryNormalizeContent_AllowsEmpty_ForAttachmentOnlyMessages()
    {
        var ok = MessagePipelinePolicy.TryNormalizeContent(string.Empty, out var normalized, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalizeContent_RejectsOverLength()
    {
        var huge = new string('a', MessagePipelinePolicy.MaxContentCharacters + 1);

        var ok = MessagePipelinePolicy.TryNormalizeContent(huge, out var normalized, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(string.Empty, normalized);
    }

    [Theory]
    [InlineData("create a quote")]
    [InlineData("I'll select PLA")]
    [InlineData("where is my order?")]
    [InlineData("please update my address")]
    [InlineData("delete this part from the cart")]
    public void TryNormalizeContent_AllowsOrdinaryManufacturingLanguage(string message)
    {
        // Regression guard: these natural-language phrases must NOT be rejected the way the
        // SQL/keyword heuristics in IInputValidationService would reject them.
        var ok = MessagePipelinePolicy.TryNormalizeContent(message, out _, out var error);

        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateAttachmentBudget_WithinLimits_Ok()
    {
        Assert.True(MessagePipelinePolicy.TryValidateAttachmentBudget(3, 5L * 1024 * 1024, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidateAttachmentBudget_TooManyAttachments_Rejected()
    {
        Assert.False(MessagePipelinePolicy.TryValidateAttachmentBudget(
            MessagePipelinePolicy.MaxAttachmentsPerMessage + 1, 1, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateAttachmentBudget_TotalTooLarge_Rejected()
    {
        Assert.False(MessagePipelinePolicy.TryValidateAttachmentBudget(
            1, MessagePipelinePolicy.MaxTotalAttachmentBytes + 1, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void AttachmentMetadata_PersistsUrlReferences_SkipsInlineData_AndRoundTrips()
    {
        var json = MessagePipelinePolicy.BuildAttachmentMetadataJson(new List<(string, string)>
        {
            ("model/step", "gs://bucket/part.step"),
            ("image/png", "https://cdn.example.com/sketch.png"),
            ("image/jpeg", "data:image/jpeg;base64,AAAA")
        });

        Assert.NotNull(json);
        var parsed = MessagePipelinePolicy.ParsePersistedAttachments(json);
        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed, a => a.Data == "gs://bucket/part.step" && a.MimeType == "model/step");
        Assert.Contains(parsed, a => a.Data == "https://cdn.example.com/sketch.png");
        Assert.DoesNotContain(parsed, a => a.Data.StartsWith("data:", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildAttachmentMetadataJson_NoPersistableReferences_ReturnsNull()
    {
        Assert.Null(MessagePipelinePolicy.BuildAttachmentMetadataJson(new List<(string, string)>
        {
            ("image/jpeg", "data:image/jpeg;base64,AAAA")
        }));
        Assert.Null(MessagePipelinePolicy.BuildAttachmentMetadataJson(null));
        Assert.Null(MessagePipelinePolicy.BuildAttachmentMetadataJson(new List<(string, string)>()));
    }

    [Fact]
    public void BuildDailyBudgetExceededMessage_IsLocalizedAndCustomerSafe()
    {
        var en = MessagePipelinePolicy.BuildDailyBudgetExceededMessage(Language.English);
        var th = MessagePipelinePolicy.BuildDailyBudgetExceededMessage(Language.Thai);

        Assert.Contains("usage limit", en, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("info@maliev.com", en);
        Assert.Contains("info@maliev.com", th);
        Assert.NotEqual(en, th);
    }

    [Fact]
    public void ParsePersistedAttachments_InvalidOrUnrelatedMetadata_ReturnsEmpty()
    {
        Assert.Empty(MessagePipelinePolicy.ParsePersistedAttachments(null));
        Assert.Empty(MessagePipelinePolicy.ParsePersistedAttachments("not json"));
        // Assistant-message metadata (a different schema) must not yield attachments.
        Assert.Empty(MessagePipelinePolicy.ParsePersistedAttachments("{\"intent\":\"sales\",\"confidence\":0.9}"));
    }
}
