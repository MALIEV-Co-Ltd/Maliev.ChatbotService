namespace Maliev.ChatbotService.Tests.Infrastructure;

public static class TestHelpers
{
    public static async Task<T> WaitForAsync<T>(
        Func<Task<T>> action,
        Func<T, bool> until,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        string? message = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        interval ??= TimeSpan.FromSeconds(1);
        var deadline = DateTimeOffset.UtcNow.Add(timeout.Value);

        T result = default!;
        while (DateTimeOffset.UtcNow < deadline)
        {
            result = await action();
            if (until(result))
            {
                return result;
            }

            await Task.Delay(interval.Value);
        }

        throw new TimeoutException(
            message ?? $"Condition not met within {timeout.Value.TotalSeconds}s. Last result: {result}");
    }
}
