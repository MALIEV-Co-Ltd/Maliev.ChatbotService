using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="FallbackResponseTemplate"/>.
/// </summary>
public class FallbackResponseTemplateConfiguration : IEntityTypeConfiguration<FallbackResponseTemplate>
{
    /// <summary>
    /// Configures the entity of type <see cref="FallbackResponseTemplate"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<FallbackResponseTemplate> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScenarioType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Language)
            .IsRequired();

        builder.Property(x => x.ResponseText)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => new { x.ScenarioType, x.Language, x.IsActive });
        builder.HasIndex(x => x.Priority);
    }
}
