using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class SyncJobConfiguration : IEntityTypeConfiguration<SyncJob>
{
    public void Configure(EntityTypeBuilder<SyncJob> builder)
    {
        builder.ToTable("SyncJobs");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.EntityType).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Direction).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.ErrorMessage).HasColumnType("text");

        builder.HasIndex(s => s.StartedAt);
    }
}
