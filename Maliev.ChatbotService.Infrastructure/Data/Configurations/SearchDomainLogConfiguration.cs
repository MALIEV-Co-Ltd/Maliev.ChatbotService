using Maliev.ChatbotService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.ChatbotService.Infrastructure.Data.Configurations;

/// <summary>
/// Entity configuration for <see cref="SearchDomainLog"/>.
/// </summary>
public class SearchDomainLogConfiguration : IEntityTypeConfiguration<SearchDomainLog>
{
    /// <summary>
    /// Configures the entity of type <see cref="SearchDomainLog"/>.
    /// </summary>
    /// <param name="builder">The builder to configure the entity.</param>
    public void Configure(EntityTypeBuilder<SearchDomainLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionId)
            .IsRequired();

        builder.Property(x => x.Domain)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SearchQuery)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.AccessedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.SessionId);

        builder.HasIndex(x => x.Domain);

        builder.HasIndex(x => x.AccessedAt);

        // Relationships
        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
