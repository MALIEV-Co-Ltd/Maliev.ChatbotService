namespace Maliev.ChatbotService.Application.Interfaces;

/// <summary>
/// Configuration for external client base addresses.
/// </summary>
public class ExternalClientsConfiguration
{
    /// <summary>
    /// Configuration for Gemini API client.
    /// </summary>
    public ClientConfiguration Gemini { get; set; } = new();

    /// <summary>
    /// Configuration for LINE API client.
    /// </summary>
    public ClientConfiguration Line { get; set; } = new();

    /// <summary>
    /// Configuration for Facebook/Meta API client.
    /// </summary>
    public ClientConfiguration Facebook { get; set; } = new();
}

/// <summary>
/// Configuration for a single external client.
/// </summary>
public class ClientConfiguration
{
    /// <summary>
    /// Base URL for the external client.
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;
}
