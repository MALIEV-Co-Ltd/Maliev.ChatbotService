namespace Maliev.ChatbotService.Tests.Unit;

public sealed class SessionExpiryBackgroundServicePerformanceTests
{
    [Fact]
    public void RegisteredSessionExpiryBackgroundService_UsesCountAggregateForClosedEventMessageCount()
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Maliev.ChatbotService.Infrastructure",
            "BackgroundServices",
            "SessionExpiryBackgroundService.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        Assert.Contains("CountBySessionIdAsync(session.Id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetBySessionIdAsync(session.Id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRecentBySessionIdAsync(session.Id", source, StringComparison.Ordinal);
    }
}
