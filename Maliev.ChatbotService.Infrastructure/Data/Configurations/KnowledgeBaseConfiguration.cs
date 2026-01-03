using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="KnowledgeBase"/>.
/// </summary>
public class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    /// <summary>
    /// Configures the entity of type <see cref="KnowledgeBase"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TopicKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FactKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.Metadata)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.TopicKey);
        builder.HasIndex(x => new { x.TopicKey, x.FactKey }).IsUnique();
    }
}
