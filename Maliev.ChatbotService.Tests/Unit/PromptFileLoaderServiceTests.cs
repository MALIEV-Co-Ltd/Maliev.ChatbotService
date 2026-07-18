using System.Reflection;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class PromptFileLoaderServiceTests
{
    [Fact]
    public async Task ProcessPromptFileAsync_MetadataOnlyChange_UpdatesExistingInstruction()
    {
        var existing = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Quote Engine Assistant",
            Category = SystemInstructionCategory.Topic,
            TopicKey = "legacy",
            Priority = 10,
            PersonaDefinition = "You are the Make Studio agent.",
            BusinessConstraints = "## Business Constraints\nUse the existing workflow.",
            AllowedTopics = "legacy",
            IsActive = false,
            Version = 4,
            EnableWebSearch = false
        };

        var repository = CreateRepository(existing);
        var filePath = await WritePromptFileAsync(
            """
            ---
            name: Quote Engine Assistant
            category: Core
            topic_key: quote-engine
            priority: 90
            is_active: true
            allowed_topics: quote-engine,manufacturing
            enable_web_search: true
            ---

            You are the Make Studio agent.

            ## Business Constraints
            Use the existing workflow.
            """);

        try
        {
            var changed = await ProcessPromptFileAsync(filePath, repository.Object);

            Assert.True(changed);
            Assert.Equal(SystemInstructionCategory.Core, existing.Category);
            Assert.Equal("quote-engine", existing.TopicKey);
            Assert.Equal(90, existing.Priority);
            Assert.Equal("quote-engine,manufacturing", existing.AllowedTopics);
            Assert.True(existing.IsActive);
            Assert.True(existing.EnableWebSearch);
            Assert.Equal(5, existing.Version);
            repository.Verify(item => item.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            DeletePromptFile(filePath);
        }
    }

    [Fact]
    public async Task ProcessPromptFileAsync_UnchangedInstruction_DoesNotUpdateExistingInstruction()
    {
        var existing = new SystemInstruction
        {
            Id = Guid.NewGuid(),
            Name = "Quote Engine Assistant",
            Category = SystemInstructionCategory.Core,
            TopicKey = "quote-engine",
            Priority = 90,
            PersonaDefinition = "You are the Make Studio agent.",
            BusinessConstraints = "## Business Constraints\nUse the existing workflow.",
            AllowedTopics = "quote-engine,manufacturing",
            IsActive = true,
            Version = 4,
            EnableWebSearch = true
        };

        var repository = CreateRepository(existing);
        var filePath = await WritePromptFileAsync(
            """
            ---
            name: Quote Engine Assistant
            category: Core
            topic_key: quote-engine
            priority: 90
            is_active: true
            allowed_topics: quote-engine,manufacturing
            enable_web_search: true
            ---

            You are the Make Studio agent.

            ## Business Constraints
            Use the existing workflow.
            """);

        try
        {
            var changed = await ProcessPromptFileAsync(filePath, repository.Object);

            Assert.False(changed);
            Assert.Equal(4, existing.Version);
            repository.Verify(item => item.UpdateAsync(It.IsAny<SystemInstruction>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            DeletePromptFile(filePath);
        }
    }

    private static Mock<ISystemInstructionRepository> CreateRepository(SystemInstruction existing)
    {
        var repository = new Mock<ISystemInstructionRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.GetAllAsync(1, 1000, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SystemInstruction> { existing }, 1));
        repository
            .Setup(item => item.UpdateAsync(existing, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return repository;
    }

    private static async Task<bool> ProcessPromptFileAsync(string filePath, ISystemInstructionRepository repository)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        var service = new PromptFileLoaderService(
            Mock.Of<IServiceProvider>(),
            hostEnvironment.Object,
            NullLogger<PromptFileLoaderService>.Instance);

        var method = typeof(PromptFileLoaderService).GetMethod(
            "ProcessPromptFileAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<bool>)method.Invoke(service, [filePath, repository, CancellationToken.None])!;
        return await task;
    }

    private static async Task<string> WritePromptFileAsync(string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Maliev.ChatbotService.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, "prompt.md");
        await File.WriteAllTextAsync(filePath, content);

        return filePath;
    }

    private static void DeletePromptFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
