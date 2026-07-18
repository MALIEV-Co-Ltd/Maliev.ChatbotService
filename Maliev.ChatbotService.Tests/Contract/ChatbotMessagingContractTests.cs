using Maliev.ChatbotService.Application.Messaging;
using Maliev.MessagingContracts.Contracts.Chatbot;
using Maliev.MessagingContracts.Contracts.Shared;

namespace Maliev.ChatbotService.Tests.Contract;

public sealed class ChatbotMessagingContractTests
{
    [Fact]
    public void PublishedEvents_UseCentralSchemaGeneratedContracts()
    {
        var sessionId = Guid.NewGuid();
        var userProfileId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var events = new BaseMessage[]
        {
            ChatbotEventFactory.SessionCreated(
                sessionId, userProfileId, "Website", "English", now, now.AddHours(24)),
            ChatbotEventFactory.SessionClosed(
                sessionId, userProfileId, "Website", now, now.AddHours(1), 3, "Expired"),
            ChatbotEventFactory.MessageReceived(
                sessionId, userProfileId, "Website", "English", "hello", "hi", 12.5, now),
            ChatbotEventFactory.RateLimitExceeded(
                sessionId, userProfileId, "Website", 101, 100, now.AddHours(1)),
        };

        Assert.Collection(
            events,
            item => Assert.IsType<ChatbotSessionCreatedEvent>(item),
            item => Assert.IsType<ChatbotSessionClosedEvent>(item),
            item => Assert.IsType<ChatbotMessageReceivedEvent>(item),
            item => Assert.IsType<ChatbotRateLimitExceededEvent>(item));

        foreach (var integrationEvent in events)
        {
            Assert.NotEqual(Guid.Empty, integrationEvent.MessageId);
            Assert.Equal(MessageType.Event, integrationEvent.MessageType);
            Assert.Equal("1.0", integrationEvent.MessageVersion);
            Assert.Equal("ChatbotService", integrationEvent.PublishedBy);
            Assert.Empty(integrationEvent.ConsumedBy);
            Assert.Equal(sessionId, integrationEvent.CorrelationId);
            Assert.Null(integrationEvent.CausationId);
            Assert.True(integrationEvent.IsPublic);
        }
    }

    [Fact]
    public void LocalDuplicateEventDefinitions_AreAbsent()
    {
        var root = FindRoot();
        var localEvents = Path.Combine(root, "Maliev.ChatbotService.Domain", "Events");

        Assert.False(Directory.Exists(localEvents) && Directory.EnumerateFiles(localEvents, "*.cs").Any());
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.ChatbotService.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ChatbotService repository root.");
    }
}
