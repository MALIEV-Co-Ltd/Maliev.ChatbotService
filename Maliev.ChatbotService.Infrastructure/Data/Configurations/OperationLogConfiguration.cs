using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="OperationLog"/>.
/// </summary>
public class OperationLogConfiguration : IEntityTypeConfiguration<OperationLog>
{
    /// <summary>
    /// Configures the entity of type <see cref="OperationLog"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<OperationLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.OperationParameters)
            .HasColumnType("jsonb");

        builder.Property(x => x.ExecutionResult)
            .HasColumnType("jsonb");

        builder.Property(x => x.ExecutedAt)
            .IsRequired();

        builder.Property(x => x.Success)
            .IsRequired();

        builder.Property(x => x.ActionSource)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("user");

        // Indexes
        builder.HasIndex(x => x.UserProfileId);

        builder.HasIndex(x => x.MessageId);

        builder.HasIndex(x => x.ExecutedAt);

        builder.HasIndex(x => new { x.UserProfileId, x.ExecutedAt });

        // Relationships
        builder.HasOne(x => x.UserProfile)
            .WithMany(x => x.OperationLogs)
            .HasForeignKey(x => x.UserProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Message)
            .WithMany(x => x.OperationLogs)
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
