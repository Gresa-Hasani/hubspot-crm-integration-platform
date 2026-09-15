using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmIntegration.Infrastructure.Persistence.Configurations;

public class EntityMappingConfiguration : IEntityTypeConfiguration<EntityMapping>
{
    public void Configure(EntityTypeBuilder<EntityMapping> builder)
    {
        builder.ToTable("EntityMappings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ExternalId).HasMaxLength(100).IsRequired();
        builder.Property(m => m.EntityType).HasConversion<string>().HasMaxLength(50);
        builder.Property(m => m.ExternalSystem).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(m => m.ExternalId);
        builder.HasIndex(m => new { m.EntityType, m.ExternalSystem, m.ExternalId }).IsUnique();
        builder.HasIndex(m => new { m.EntityType, m.InternalId, m.ExternalSystem }).IsUnique();
    }
}
