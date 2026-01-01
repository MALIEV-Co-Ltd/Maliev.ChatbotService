using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="IdentityLink"/>.
/// </summary>
public class IdentityLinkConfiguration : IEntityTypeConfiguration<IdentityLink>
{
    /// <summary>
    /// Configures the entity of type <see cref="IdentityLink"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<IdentityLink> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserProfileId)
            .IsRequired();

        builder.Property(x => x.PlatformName)
            .IsRequired();

        builder.Property(x => x.ExternalPlatformId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.WebhookConfirmationStatus)
            .IsRequired();

        builder.Property(x => x.LinkCreatedAt)
            .IsRequired();

        builder.Property(x => x.LastVerifiedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.UserProfileId);

        builder.HasIndex(x => x.ExternalPlatformId);

        builder.HasIndex(x => new { x.PlatformName, x.ExternalPlatformId })
            .IsUnique();

        // Relationships
        builder.HasOne(x => x.UserProfile)
            .WithMany(x => x.IdentityLinks)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
