using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="ConversationSummaryBatchItem"/>.
/// </summary>
public class ConversationSummaryBatchItemConfiguration : IEntityTypeConfiguration<ConversationSummaryBatchItem>
{
    /// <summary>
    /// Configures the entity of type <see cref="ConversationSummaryBatchItem"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<ConversationSummaryBatchItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BatchJobId)
            .IsRequired();

        builder.Property(x => x.SessionId)
            .IsRequired();

        builder.Property(x => x.UserProfileId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.StructuredSummary)
            .HasColumnType("jsonb");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.TokenUsageJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.BatchJobId);

        builder.HasIndex(x => x.SessionId);

        builder.HasIndex(x => x.UserProfileId);

        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UserProfile)
            .WithMany()
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
