using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class IntegrationEventConfiguration : IEntityTypeConfiguration<IntegrationEvent>
{
    public void Configure(EntityTypeBuilder<IntegrationEvent> builder)
    {
        builder.ToTable("IntegrationEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExternalEventId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityType).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.Payload).HasColumnType("jsonb");

        // Idempotency: the same HubSpot event delivered twice must resolve to one row (section 12).
        builder.HasIndex(e => e.ExternalEventId).IsUnique();
        builder.HasIndex(e => e.Status);
    }
}
