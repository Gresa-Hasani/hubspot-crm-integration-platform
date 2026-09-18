using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Reporting;
using CrmIntegration.Application.Security;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Application.Sync.Services;
using CrmIntegration.Application.Webhooks;
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

        services.AddOptions<WebhookOptions>()
            .Bind(configuration.GetSection(WebhookOptions.SectionName));
        services.AddScoped<IHubSpotWebhookSignatureValidator, HubSpotWebhookSignatureValidator>();
        services.AddScoped<IWebhookIngestionService, WebhookIngestionService>();
        services.AddScoped<IIntegrationEventProcessor, IntegrationEventProcessor>();
        services.AddScoped<IIntegrationEventRetryService, IntegrationEventRetryService>();

        services.AddScoped<IAutomationExecutor, AutomationExecutor>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IDealStageAutomationService, DealStageAutomationService>();
        services.AddScoped<IContactLifecycleAutomationService, ContactLifecycleAutomationService>();
        services.AddScoped<IAutomationRetryService, AutomationRetryService>();

        services.AddScoped<ISalesReportingService, SalesReportingService>();
        services.AddScoped<IOperationsReportingService, OperationsReportingService>();

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();

        return services;
    }
}
