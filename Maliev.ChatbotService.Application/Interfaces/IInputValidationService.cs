namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Service interface for input validation and security checks.
/// </summary>
public interface IInputValidationService
{
    /// <summary>
    /// Validates user input for security threats (SQL injection, XSS, command injection).
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <returns>True if the input is safe; otherwise, false.</returns>
    bool ValidateInput(string input);

    /// <summary>
    /// Sanitizes user input by removing potentially dangerous characters.
    /// </summary>
    /// <param name="input">The input to sanitize.</param>
    /// <returns>The sanitized input.</returns>
    string SanitizeInput(string input);

    /// <summary>
    /// Validates user input asynchronously for security threats.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the input is safe; otherwise, false.</returns>
    Task<bool> ValidateInputAsync(string input, CancellationToken cancellationToken = default);
}
