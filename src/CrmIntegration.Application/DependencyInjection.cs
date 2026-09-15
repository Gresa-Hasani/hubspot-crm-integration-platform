using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Application.Sync.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegration.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDealService, DealService>();

        services.AddSingleton<IHubSpotStageMapper, HubSpotStageMapper>();

        services.AddOptions<SyncRetryOptions>()
            .Bind(configuration.GetSection(SyncRetryOptions.SectionName));
        services.AddSingleton<IRetryDelayProvider, TaskDelayProvider>();
        services.AddScoped<IHubSpotRetryExecutor, HubSpotRetryExecutor>();
        services.AddScoped<ISyncJobExecutor, SyncJobExecutor>();

        services.AddScoped<IContactHubSpotMapper, ContactHubSpotMapper>();
        services.AddScoped<ICompanyHubSpotMapper, CompanyHubSpotMapper>();
        services.AddScoped<IDealHubSpotMapper, DealHubSpotMapper>();

        services.AddScoped<IContactMatchService, ContactMatchService>();
        services.AddScoped<ICompanyMatchService, CompanyMatchService>();

        services.AddScoped<IContactSyncService, ContactSyncService>();
        services.AddScoped<ICompanySyncService, CompanySyncService>();
        services.AddScoped<IDealSyncService, DealSyncService>();
        services.AddScoped<ISyncJobRetryService, SyncJobRetryService>();

        return services;
    }
}
