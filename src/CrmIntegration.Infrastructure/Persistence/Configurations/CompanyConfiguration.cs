using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(300).IsRequired();
        builder.Property(c => c.Domain).HasMaxLength(255);
        builder.Property(c => c.Industry).HasMaxLength(150);
        builder.Property(c => c.Country).HasMaxLength(100);
        builder.Property(c => c.HubSpotId).HasMaxLength(100);

        builder.HasIndex(c => c.Domain);
        builder.HasIndex(c => c.HubSpotId).IsUnique().HasFilter("\"HubSpotId\" IS NOT NULL");
    }
}
