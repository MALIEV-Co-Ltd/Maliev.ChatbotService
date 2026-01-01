using System.Text.RegularExpressions;
using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for input validation and security checks (SQL injection, XSS, command injection prevention).
/// </summary>
public partial class InputValidationService : IInputValidationService
{
    private readonly ILogger<InputValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputValidationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public InputValidationService(ILogger<InputValidationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool ValidateInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        // Check for SQL injection patterns
        if (SqlInjectionRegex().IsMatch(input))
        {
            _logger.LogWarning("Potential SQL injection detected in input");
            return false;
        }

        // Check for XSS patterns
        if (XssRegex().IsMatch(input))
        {
            _logger.LogWarning("Potential XSS attack detected in input");
            return false;
        }

        // Check for command injection patterns
        if (CommandInjectionRegex().IsMatch(input))
        {
            _logger.LogWarning("Potential command injection detected in input");
            return false;
        }

        // Check for path traversal patterns
        if (PathTraversalRegex().IsMatch(input))
        {
            _logger.LogWarning("Potential path traversal detected in input");
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Remove potentially dangerous characters while preserving legitimate content
        var sanitized = input;

        // Remove null bytes
        sanitized = sanitized.Replace("\0", string.Empty);

        // Trim excessive whitespace
        sanitized = ExcessiveWhitespaceRegex().Replace(sanitized, " ");

        return sanitized.Trim();
    }

    /// <inheritdoc/>
    public Task<bool> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        // Delegate to synchronous method and wrap in Task
        return Task.FromResult(ValidateInput(input));
    }

    [GeneratedRegex(@"('|(--)|;|\/\*|\*\/|xp_|sp_|exec|execute|drop|create|alter|insert|update|delete|union|select|from|where)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlInjectionRegex();

    [GeneratedRegex(@"(<script|<iframe|<object|<embed|javascript:|onerror=|onload=)", RegexOptions.IgnoreCase)]
    private static partial Regex XssRegex();

    [GeneratedRegex(@"(\||&|;|`|\$\(|>\||<\||&&|\|\|)", RegexOptions.IgnoreCase)]
    private static partial Regex CommandInjectionRegex();

    [GeneratedRegex(@"(\.\./|\.\.\\|%2e%2e%2f|%2e%2e/|%2e%2e\\|\.\.%2f|\.\.%5c)", RegexOptions.IgnoreCase)]
    private static partial Regex PathTraversalRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex ExcessiveWhitespaceRegex();
}
