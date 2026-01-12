using Maliev.ChatbotService.Infrastructure.Data;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.ChatbotService.Tests.Integration;

/// <summary>
/// Tests for InputValidationService with SQL injection, XSS, and command injection prevention.
/// </summary>
[Collection("Database")]
public class InputValidationServiceTests : IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, ChatbotDbContext> _factory;

    /// <summary>
    /// Test constructor.
    /// </summary>
    public InputValidationServiceTests(BaseIntegrationTestFactory<Program, ChatbotDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Initializes the test.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;
    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
    /// <summary>
    /// Tests that SQL injection attempts are detected.
    /// </summary>
    [Fact]
    public async Task ValidateInputAsync_SqlInjection_ReturnsDangerous()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInputValidationService>();
        var maliciousInput = "'; DROP TABLE Users; --";

        // Act
        var result = await service.ValidateInputAsync(maliciousInput);

        // Assert
        Assert.False(result);

    }

    /// <summary>
    /// Tests that XSS attempts are detected.
    /// </summary>
    [Fact]
    public async Task ValidateInputAsync_XssAttack_ReturnsDangerous()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInputValidationService>();
        var maliciousInput = "<script>alert('XSS')</script>";

        // Act
        var result = await service.ValidateInputAsync(maliciousInput);

        // Assert
        Assert.False(result);

    }

    /// <summary>
    /// Tests that command injection attempts are detected.
    /// </summary>
    [Fact]
    public async Task ValidateInputAsync_CommandInjection_ReturnsDangerous()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInputValidationService>();
        var maliciousInput = "test && rm -rf /";

        // Act
        var result = await service.ValidateInputAsync(maliciousInput);

        // Assert
        Assert.False(result);

    }

    /// <summary>
    /// Tests that safe input is allowed.
    /// </summary>
    [Fact]
    public async Task ValidateInputAsync_SafeInput_ReturnsValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInputValidationService>();
        var safeInput = "What manufacturing services do you offer?";

        // Act
        var result = await service.ValidateInputAsync(safeInput);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests that Thai language input is allowed.
    /// </summary>
    [Fact]
    public async Task ValidateInputAsync_ThaiInput_ReturnsValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInputValidationService>();
        var thaiInput = "สวัสดีครับ ต้องการสอบถามเกี่ยวกับบริการผลิต";

        // Act
        var result = await service.ValidateInputAsync(thaiInput);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests that path traversal attempts are detected.
    /// </summary>
    [Fact]
    public async Task ValidateInputAsync_PathTraversal_ReturnsDangerous()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInputValidationService>();
        var maliciousInput = "../../../etc/passwd";

        // Act
        var result = await service.ValidateInputAsync(maliciousInput);

        // Assert
        Assert.False(result);

    }
}

