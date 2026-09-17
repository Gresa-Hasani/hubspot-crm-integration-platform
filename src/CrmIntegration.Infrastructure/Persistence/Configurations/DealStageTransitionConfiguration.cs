using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class DealStageTransitionConfiguration : IEntityTypeConfiguration<DealStageTransition>
{
    public void Configure(EntityTypeBuilder<DealStageTransition> builder)
    {
        builder.ToTable("DealStageTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStage).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.ToStage).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.CorrelationId).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.DealId);
        builder.HasIndex(t => t.OccurredAt);

        builder.HasOne(t => t.Deal)
            .WithMany()
            .HasForeignKey(t => t.DealId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
