using System.Net.Http.Json;
using System.Text.Json;

namespace Maliev.ChatbotService.Infrastructure.Tools.Handlers;

/// <summary>
/// Base class for tool handlers that call microservice APIs via named HttpClients.
/// </summary>
public abstract class BaseToolHandler : IToolHandler
{
    /// <summary>
    /// The HTTP client factory used to create named clients.
    /// </summary>
    protected readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Gets the named HttpClient service identifier for the target microservice.
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseToolHandler"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to create named clients.</param>
    protected BaseToolHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public abstract Task<string> ExecuteAsync(string toolName, Dictionary<string, object> args, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a GET request to the microservice and returns the response body.
    /// </summary>
    /// <param name="path">The relative request path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response content as a string.</returns>
    protected async Task<string> GetAsync(string path, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ServiceName);
        var response = await client.GetAsync(path, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return JsonSerializer.Serialize(new { error = $"API returned {response.StatusCode}", detail = content });
        }

        return content;
    }

    /// <summary>
    /// Sends a POST request to the microservice and returns the response body.
    /// </summary>
    /// <param name="path">The relative request path.</param>
    /// <param name="payload">The payload object to serialize as JSON.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response content as a string.</returns>
    protected async Task<string> PostAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(ServiceName);
        var response = await client.PostAsJsonAsync(path, payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return JsonSerializer.Serialize(new { error = $"API returned {response.StatusCode}", detail = content });
        }

        return content;
    }

    /// <summary>
    /// Retrieves a string argument from the arguments dictionary.
    /// </summary>
    /// <param name="args">The arguments dictionary.</param>
    /// <param name="key">The argument key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The string value of the argument.</returns>
    protected static string GetStringArg(Dictionary<string, object> args, string key, string defaultValue = "")
    {
        if (args.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Retrieves an integer argument from the arguments dictionary.
    /// </summary>
    /// <param name="args">The arguments dictionary.</param>
    /// <param name="key">The argument key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <returns>The integer value of the argument.</returns>
    protected static int GetIntArg(Dictionary<string, object> args, string key, int defaultValue = 1)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is double d) return (int)d;
            if (value is int i) return i;
            if (int.TryParse(value?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }
}
