using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="UserProfile"/>.
/// </summary>
public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    /// <summary>
    /// Configures the entity of type <see cref="UserProfile"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InternalUserId)
            .HasMaxLength(100);

        builder.Property(x => x.LineUserId)
            .HasMaxLength(100);

        builder.Property(x => x.FacebookId)
            .HasMaxLength(100);

        builder.Property(x => x.InstagramId)
            .HasMaxLength(100);

        builder.Property(x => x.WhatsAppId)
            .HasMaxLength(100);

        builder.Property(x => x.Role)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastActiveAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.LineUserId)
            .IsUnique()
            .HasFilter("[LineUserId] IS NOT NULL");

        builder.HasIndex(x => x.FacebookId)
            .IsUnique()
            .HasFilter("[FacebookId] IS NOT NULL");

        builder.HasIndex(x => x.InstagramId)
            .IsUnique()
            .HasFilter("[InstagramId] IS NOT NULL");

        builder.HasIndex(x => x.WhatsAppId)
            .IsUnique()
            .HasFilter("[WhatsAppId] IS NOT NULL");

        builder.HasIndex(x => x.InternalUserId)
            .HasFilter("[InternalUserId] IS NOT NULL");

        // Relationships
        builder.HasMany(x => x.Sessions)
            .WithOne(x => x.UserProfile)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Memories)
            .WithOne(x => x.UserProfile)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.IdentityLinks)
            .WithOne(x => x.UserProfile)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.OperationLogs)
            .WithOne(x => x.UserProfile)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
