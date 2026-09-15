using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("Deals");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(300).IsRequired();
        builder.Property(d => d.HubSpotId).HasMaxLength(100);
        builder.Property(d => d.Currency).HasMaxLength(3).IsRequired();
        builder.Property(d => d.Owner).HasMaxLength(200);
        builder.Property(d => d.Amount).HasColumnType("numeric(18,2)");
        builder.Property(d => d.Stage).HasConversion<string>().HasMaxLength(50);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(d => d.HubSpotId).IsUnique().HasFilter("\"HubSpotId\" IS NOT NULL");
        builder.HasIndex(d => d.Stage);

        builder.HasOne(d => d.Company)
            .WithMany(c => c.Deals)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Contact)
            .WithMany()
            .HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
