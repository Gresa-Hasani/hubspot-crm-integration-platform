using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class OnboardingRecordConfiguration : IEntityTypeConfiguration<OnboardingRecord>
{
    public void Configure(EntityTypeBuilder<OnboardingRecord> builder)
    {
        builder.ToTable("OnboardingRecords");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.TriggerSource).HasMaxLength(200);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(50);

        // Prevent duplicate onboarding creation for the same deal (Automation 1, section 16).
        builder.HasIndex(o => o.DealId).IsUnique();

        builder.HasOne(o => o.Deal)
            .WithMany()
            .HasForeignKey(o => o.DealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Company)
            .WithMany()
            .HasForeignKey(o => o.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
