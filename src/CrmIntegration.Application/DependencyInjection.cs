using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegration.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDealService, DealService>();

        services.AddSingleton<IHubSpotStageMapper, HubSpotStageMapper>();

        return services;
    }
}
