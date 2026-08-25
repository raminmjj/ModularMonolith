using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Brand.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBrandApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.IBrandService, Service.BrandService>();
        services.AddScoped<Ports.Inbound.IBrandAdminService, Service.BrandAdminService>();
        return services;
    }
}
