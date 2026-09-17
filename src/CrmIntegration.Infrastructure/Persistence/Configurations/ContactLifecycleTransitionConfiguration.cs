using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class ContactLifecycleTransitionConfiguration : IEntityTypeConfiguration<ContactLifecycleTransition>
{
    public void Configure(EntityTypeBuilder<ContactLifecycleTransition> builder)
    {
        builder.ToTable("ContactLifecycleTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStage).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.ToStage).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.CorrelationId).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.ContactId);
        builder.HasIndex(t => t.OccurredAt);

        builder.HasOne(t => t.Contact)
            .WithMany()
            .HasForeignKey(t => t.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
