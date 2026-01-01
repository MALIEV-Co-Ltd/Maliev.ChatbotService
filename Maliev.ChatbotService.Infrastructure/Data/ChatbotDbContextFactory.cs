using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.ChatbotService.Infrastructure.Data;

/// <summary>
/// Design-time factory for ChatbotDbContext to support EF Core migrations.
/// </summary>
public class ChatbotDbContextFactory : IDesignTimeDbContextFactory<ChatbotDbContext>
{
    /// <summary>
    /// Creates a new instance of ChatbotDbContext for design-time operations.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A configured ChatbotDbContext instance.</returns>
    public ChatbotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChatbotDbContext>();

        // Use a placeholder connection string for design-time operations
        optionsBuilder.UseNpgsql("Host=localhost;Database=chatbot_app_db;Username=postgres;Password=postgres");

        return new ChatbotDbContext(optionsBuilder.Options);
    }
}
