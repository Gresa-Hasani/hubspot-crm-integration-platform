using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.JobTitle).HasMaxLength(200);
        builder.Property(c => c.HubSpotId).HasMaxLength(100);
        builder.Property(c => c.Source).HasMaxLength(100);
        builder.Property(c => c.LifecycleStage).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(c => c.Email);
        builder.HasIndex(c => c.HubSpotId).IsUnique().HasFilter("\"HubSpotId\" IS NOT NULL");

        builder.HasOne(c => c.Company)
            .WithMany(co => co.Contacts)
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
