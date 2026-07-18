using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="ConversationSummary"/>.
/// </summary>
public class ConversationSummaryConfiguration : IEntityTypeConfiguration<ConversationSummary>
{
    /// <summary>
    /// Configures the entity of type <see cref="ConversationSummary"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<ConversationSummary> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionId)
            .IsRequired();

        builder.Property(x => x.UserProfileId)
            .IsRequired();

        builder.Property(x => x.StructuredSummary)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.SessionId)
            .IsUnique();

        builder.HasIndex(x => x.UserProfileId);

        // Relationships
        builder.HasOne(x => x.UserProfile)
            .WithMany()
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Session)
            .WithOne(x => x.Summary)
            .HasForeignKey<ConversationSummary>(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
