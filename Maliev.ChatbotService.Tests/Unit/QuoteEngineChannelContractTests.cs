using System.ComponentModel.DataAnnotations;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Domain.Enums;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class QuoteEngineChannelContractTests
{
    [Fact]
    public void Channel_enum_contains_quote_engine_channel()
    {
        Assert.True(Enum.IsDefined(typeof(Channel), "QuoteEngine"));
    }

    [Fact]
    public void InitiateSessionRequest_accepts_quote_engine_channel()
    {
        var request = new InitiateSessionRequest
        {
            Channel = "quote-engine",
            Language = "en"
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.True(valid, string.Join("; ", results.Select(result => result.ErrorMessage)));
    }

    [Fact]
    public void SendMessageRequest_carries_signed_quote_agent_context()
    {
        var request = new SendMessageRequest
        {
            SessionId = Guid.NewGuid(),
            Content = "Price this bracket.",
            QuoteAgentContextToken = "signed-context-token"
        };

        Assert.Equal("signed-context-token", request.QuoteAgentContextToken);
    }

    [Fact]
    public void SendMessageRequest_accepts_small_model_override_for_utility_calls()
    {
        var request = new SendMessageRequest
        {
            SessionId = Guid.NewGuid(),
            Content = "Clean dictated text.",
            ModelName = "gemini-2.5-flash-lite"
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);

        Assert.True(valid, string.Join("; ", results.Select(result => result.ErrorMessage)));
        Assert.Equal("gemini-2.5-flash-lite", request.ModelName);
    }
}
