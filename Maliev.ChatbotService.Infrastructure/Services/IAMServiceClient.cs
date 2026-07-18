using Maliev.ChatbotService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Client for IAM Service operations.
/// </summary>
public class IAMServiceClient : IIAMServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IAMServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IAMServiceClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public IAMServiceClient(HttpClient httpClient, ILogger<IAMServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/iam/v1/users/{userId}/permissions/{permission}/check", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to check permission for user {UserId}: {StatusCode}", userId, response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return result.TryGetProperty("hasPermission", out var hasPerm) && hasPerm.GetBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/iam/v1/users/{userId}/permissions", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get permissions for user {UserId}: {StatusCode}", userId, response.StatusCode);
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            if (result.TryGetProperty("permissions", out var permissions))
            {
                return permissions.EnumerateArray()
                    .Select(p => p.GetString() ?? string.Empty)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user {UserId}", userId);
            return new List<string>();
        }
    }
}
