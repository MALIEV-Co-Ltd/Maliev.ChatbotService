using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="UserMemory"/>.
/// </summary>
public class UserMemoryConfiguration : IEntityTypeConfiguration<UserMemory>
{
    /// <summary>
    /// Configures the entity of type <see cref="UserMemory"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<UserMemory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserProfileId)
            .IsRequired();

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Value)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.Confidence)
            .IsRequired();

        builder.Property(x => x.SourceMessageId)
            .IsRequired();

        builder.Property(x => x.LastUpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.UserProfileId);

        builder.HasIndex(x => new { x.UserProfileId, x.Key });

        builder.HasIndex(x => x.Confidence);

        // Relationships
        builder.HasOne(x => x.UserProfile)
            .WithMany(x => x.Memories)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SourceMessage)
            .WithMany()
            .HasForeignKey(x => x.SourceMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
