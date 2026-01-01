using Xunit;

using Maliev.ChatbotService.Infrastructure.Data;

namespace Maliev.ChatbotService.Tests.Infrastructure;

/// <summary>
/// XUnit collection definition for database-dependent tests.
/// Ensures all tests in this collection share the same test factory instance.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<BaseIntegrationTestFactory<Program, ChatbotDbContext>>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

