using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(150).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Source).HasMaxLength(100).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Metadata).HasColumnType("jsonb");

        builder.HasIndex(a => a.CorrelationId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
