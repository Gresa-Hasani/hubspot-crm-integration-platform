using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegration.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDealService, DealService>();

        return services;
    }
}
