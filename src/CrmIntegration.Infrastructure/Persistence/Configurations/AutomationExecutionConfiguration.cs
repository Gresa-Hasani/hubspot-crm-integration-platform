using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class AutomationExecutionConfiguration : IEntityTypeConfiguration<AutomationExecution>
{
    public void Configure(EntityTypeBuilder<AutomationExecution> builder)
    {
        builder.ToTable("AutomationExecutions");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AutomationType).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.EntityType).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FailureCategory).HasMaxLength(100);
        builder.Property(a => a.ResultSummary).HasMaxLength(500);

        // Database-level idempotency backstop — see docs/SALES_AUTOMATION.md.
        builder.HasIndex(a => a.IdempotencyKey).IsUnique();
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.Status);
    }
}
