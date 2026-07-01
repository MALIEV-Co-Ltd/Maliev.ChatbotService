using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class FileValidationServiceTests
{
    private readonly FileValidationService _service;

    public FileValidationServiceTests()
    {
        var loggerMock = new Mock<ILogger<FileValidationService>>();
        _service = new FileValidationService(loggerMock.Object);
    }

    // GetContentTypeFromMimeType tests

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetContentTypeFromMimeType_WithNullOrEmpty_ReturnsText(string? mimeType)
    {
        var result = _service.GetContentTypeFromMimeType(mimeType!);
        Assert.Equal(ContentType.Text, result);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("IMAGE/PNG")]
    public void GetContentTypeFromMimeType_WithImageMime_ReturnsImage(string mimeType)
    {
        var result = _service.GetContentTypeFromMimeType(mimeType);
        Assert.Equal(ContentType.Image, result);
    }

    [Fact]
    public void GetContentTypeFromMimeType_WithPdf_ReturnsPdf()
    {
        Assert.Equal(ContentType.PDF, _service.GetContentTypeFromMimeType("application/pdf"));
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/avi")]
    public void GetContentTypeFromMimeType_WithVideoMime_ReturnsVideo(string mimeType)
    {
        Assert.Equal(ContentType.Video, _service.GetContentTypeFromMimeType(mimeType));
    }

    [Theory]
    [InlineData("audio/mp3")]
    [InlineData("audio/wav")]
    public void GetContentTypeFromMimeType_WithAudioMime_ReturnsAudio(string mimeType)
    {
        Assert.Equal(ContentType.Audio, _service.GetContentTypeFromMimeType(mimeType));
    }

    [Fact]
    public void GetContentTypeFromMimeType_WithUnknownMime_ReturnsText()
    {
        Assert.Equal(ContentType.Text, _service.GetContentTypeFromMimeType("text/plain"));
    }

    // ValidateAttachment tests

    [Fact]
    public void ValidateAttachment_WithValidImage_ReturnsValid()
    {
        var (isValid, error) = _service.ValidateAttachment("image/png", 1 * 1024 * 1024, ContentType.Image);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAttachment_WithOversizedImage_ReturnsInvalid()
    {
        var (isValid, error) = _service.ValidateAttachment("image/png", 15 * 1024 * 1024, ContentType.Image);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateAttachment_WithValidPdf_ReturnsValid()
    {
        var (isValid, error) = _service.ValidateAttachment("application/pdf", 5 * 1024 * 1024, ContentType.PDF);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAttachment_WithOversizedPdf_ReturnsInvalid()
    {
        var (isValid, error) = _service.ValidateAttachment("application/pdf", 25 * 1024 * 1024, ContentType.PDF);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateAttachment_WithValidVideo_ReturnsValid()
    {
        var (isValid, error) = _service.ValidateAttachment("video/mp4", 10 * 1024 * 1024, ContentType.Video);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAttachment_WithOversizedVideo_ReturnsInvalid()
    {
        var (isValid, error) = _service.ValidateAttachment("video/mp4", 60 * 1024 * 1024, ContentType.Video);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateAttachment_WithValidAudio_ReturnsValid()
    {
        var (isValid, error) = _service.ValidateAttachment("audio/mp3", 5 * 1024 * 1024, ContentType.Audio);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAttachment_WithOversizedAudio_ReturnsInvalid()
    {
        var (isValid, error) = _service.ValidateAttachment("audio/mp3", 25 * 1024 * 1024, ContentType.Audio);
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateAttachment_WithAudioOver10Mb_ReturnsInvalid()
    {
        var (isValid, error) = _service.ValidateAttachment("audio/mp3", 11 * 1024 * 1024, ContentType.Audio);
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("10MB", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAttachment_WithMimeMismatch_ReturnsInvalid()
    {
        // Declare as Image but send text/plain
        var (isValid, error) = _service.ValidateAttachment("text/plain", 100, ContentType.Image);
        Assert.False(isValid);
        Assert.NotNull(error);
    }
}
