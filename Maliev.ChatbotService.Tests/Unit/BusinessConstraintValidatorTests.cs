using Maliev.ChatbotService.Application.Validators;
using Maliev.ChatbotService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class BusinessConstraintValidatorTests
{
    private readonly BusinessConstraintValidator _validator;

    public BusinessConstraintValidatorTests()
    {
        var loggerMock = new Mock<ILogger<BusinessConstraintValidator>>();
        _validator = new BusinessConstraintValidator(loggerMock.Object);
    }

    private static SystemInstruction MakeInstruction(string allowedTopics = "", string rejectionTemplates = "")
        => new() { AllowedTopics = allowedTopics, RejectionTemplates = rejectionTemplates };

    // IsOnTopic tests

    [Fact]
    public void IsOnTopic_WithNoAllowedTopics_ReturnsTrue()
    {
        var result = _validator.IsOnTopic("any message", MakeInstruction(allowedTopics: ""));
        Assert.True(result);
    }

    [Fact]
    public void IsOnTopic_WhenMessageContainsTopic_ReturnsTrue()
    {
        var result = _validator.IsOnTopic("I need help with material selection", MakeInstruction("material,order,quotation"));
        Assert.True(result);
    }

    [Fact]
    public void IsOnTopic_WhenMessageDoesNotContainAnyTopic_ReturnsFalse()
    {
        var result = _validator.IsOnTopic("what is the weather today?", MakeInstruction("material,order,quotation"));
        Assert.False(result);
    }

    [Fact]
    public void IsOnTopic_WithCaseInsensitiveMatch_ReturnsTrue()
    {
        var result = _validator.IsOnTopic("I want ORDER details", MakeInstruction("order"));
        Assert.True(result);
    }

    // GetRejectionMessage tests

    [Fact]
    public void GetRejectionMessage_WithNoTemplates_ReturnsDefault()
    {
        var result = _validator.GetRejectionMessage("some message", MakeInstruction(rejectionTemplates: ""));
        Assert.Contains("manufacturing", result);
    }

    [Fact]
    public void GetRejectionMessage_WithWeatherMessage_ReturnsWeatherTemplate()
    {
        var templates = """{"weather": "I can't help with weather.", "general": "Out of scope."}""";
        var result = _validator.GetRejectionMessage("What is the weather?", MakeInstruction(rejectionTemplates: templates));
        Assert.Equal("I can't help with weather.", result);
    }

    [Fact]
    public void GetRejectionMessage_WithCompetitorMessage_ReturnsCompetitorTemplate()
    {
        var templates = """{"competitor": "I only discuss our products.", "general": "Out of scope."}""";
        var result = _validator.GetRejectionMessage("How does your pricing compare to a competitor?", MakeInstruction(rejectionTemplates: templates));
        Assert.Equal("I only discuss our products.", result);
    }

    [Fact]
    public void GetRejectionMessage_WithGeneralMessage_ReturnsGeneralTemplate()
    {
        var templates = """{"general": "Please ask about manufacturing."}""";
        var result = _validator.GetRejectionMessage("What is 2+2?", MakeInstruction(rejectionTemplates: templates));
        Assert.Equal("Please ask about manufacturing.", result);
    }

    [Fact]
    public void GetRejectionMessage_WithInvalidJson_ReturnsDefault()
    {
        var result = _validator.GetRejectionMessage("some message", MakeInstruction(rejectionTemplates: "{invalid json}"));
        Assert.Contains("manufacturing", result);
    }

    [Fact]
    public void GetRejectionMessage_WithNullDeserializeResult_ReturnsDefault()
    {
        var result = _validator.GetRejectionMessage("some message", MakeInstruction(rejectionTemplates: "null"));
        Assert.Contains("manufacturing", result);
    }

    // IsResponseCompliant tests

    [Fact]
    public void IsResponseCompliant_WithEmptyResponse_ReturnsFalse()
    {
        Assert.False(_validator.IsResponseCompliant("", MakeInstruction()));
    }

    [Fact]
    public void IsResponseCompliant_WithWhitespaceResponse_ReturnsFalse()
    {
        Assert.False(_validator.IsResponseCompliant("   ", MakeInstruction()));
    }

    [Fact]
    public void IsResponseCompliant_WithCompliantResponse_ReturnsTrue()
    {
        Assert.True(_validator.IsResponseCompliant("Here is the order status you requested.", MakeInstruction()));
    }

    [Theory]
    [InlineData("I don't know the answer")]
    [InlineData("I cannot help with that")]
    [InlineData("That is outside my expertise")]
    [InlineData("That is not my area")]
    public void IsResponseCompliant_WithBannedPhrase_ReturnsFalse(string response)
    {
        Assert.False(_validator.IsResponseCompliant(response, MakeInstruction()));
    }
}
