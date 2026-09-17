using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<OnboardingRecord> OnboardingRecords => Set<OnboardingRecord>();
    public DbSet<EntityMapping> EntityMappings => Set<EntityMapping>();
    public DbSet<IntegrationEvent> IntegrationEvents => Set<IntegrationEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<DealStageTransition> DealStageTransitions => Set<DealStageTransition>();
    public DbSet<ContactLifecycleTransition> ContactLifecycleTransitions => Set<ContactLifecycleTransition>();
    public DbSet<AutomationExecution> AutomationExecutions => Set<AutomationExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
