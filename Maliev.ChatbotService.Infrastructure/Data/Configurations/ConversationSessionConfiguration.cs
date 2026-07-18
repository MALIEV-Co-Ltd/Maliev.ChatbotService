using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="ConversationSession"/>.
/// </summary>
public class ConversationSessionConfiguration : IEntityTypeConfiguration<ConversationSession>
{
    /// <summary>
    /// Configures the entity of type <see cref="ConversationSession"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<ConversationSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserProfileId)
            .IsRequired();

        builder.Property(x => x.Channel)
            .IsRequired();

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.LastActivityAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.Language)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.UserProfileId);

        builder.HasIndex(x => x.ExpiresAt);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => new { x.UserProfileId, x.Channel, x.Status });

        // Relationships
        builder.HasOne(x => x.UserProfile)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Summary)
            .WithOne(x => x.Session)
            .HasForeignKey<ConversationSession>(x => x.SummaryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
