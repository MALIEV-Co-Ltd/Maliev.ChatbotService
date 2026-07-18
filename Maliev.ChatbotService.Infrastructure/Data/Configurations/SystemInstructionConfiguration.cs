using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="SystemInstruction"/>.
/// </summary>
public class SystemInstructionConfiguration : IEntityTypeConfiguration<SystemInstruction>
{
    /// <summary>
    /// Configures the entity of type <see cref="SystemInstruction"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<SystemInstruction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.TopicKey)
            .HasMaxLength(100);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.PersonaDefinition)
            .IsRequired();

        builder.Property(x => x.BusinessConstraints)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.Version)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.IsActive);

        builder.HasIndex(x => x.TopicKey);

        builder.HasIndex(x => new { x.Name, x.Version });
    }
}
