using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Infrastructure.Persistence;
using CrmIntegration.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
