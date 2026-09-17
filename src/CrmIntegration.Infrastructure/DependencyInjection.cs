using System.Net.Http.Headers;
using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.Infrastructure.HubSpot;
using CrmIntegration.Infrastructure.Persistence;
using CrmIntegration.Infrastructure.Persistence.Repositories;
using CrmIntegration.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Default' configuration.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddOptions<HubSpotOptions>()
            .Bind(configuration.GetSection(HubSpotOptions.SectionName));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IDealRepository, DealRepository>();
        services.AddScoped<IEntityMappingRepository, EntityMappingRepository>();
        services.AddScoped<ISyncJobRepository, SyncJobRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IIntegrationEventRepository, IntegrationEventRepository>();
        services.AddScoped<IDealStageTransitionRepository, DealStageTransitionRepository>();
        services.AddScoped<IContactLifecycleTransitionRepository, ContactLifecycleTransitionRepository>();
        services.AddScoped<IAutomationExecutionRepository, AutomationExecutionRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();

        services.AddHttpClient<IHubSpotClient, HubSpotClient>((serviceProvider, client) =>
        {
            var hubSpotOptions = serviceProvider.GetRequiredService<IOptions<HubSpotOptions>>().Value;
            client.BaseAddress = new Uri(hubSpotOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(hubSpotOptions.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hubSpotOptions.AccessToken);
        });

        services.AddSingleton<IWebhookPayloadParser, WebhookPayloadParser>();
        services.AddHostedService<IntegrationEventProcessingWorker>();

        return services;
    }
}
