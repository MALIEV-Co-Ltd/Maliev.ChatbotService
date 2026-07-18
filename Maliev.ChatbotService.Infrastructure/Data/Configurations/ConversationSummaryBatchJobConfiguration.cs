using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="ConversationSummaryBatchJob"/>.
/// </summary>
public class ConversationSummaryBatchJobConfiguration : IEntityTypeConfiguration<ConversationSummaryBatchJob>
{
    /// <summary>
    /// Configures the entity of type <see cref="ConversationSummaryBatchJob"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<ConversationSummaryBatchJob> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BatchName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ModelName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.BatchName)
            .IsUnique();

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.CreatedAt);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.BatchJob)
            .HasForeignKey(x => x.BatchJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
