using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Brand.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddBrandQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<IBrandQueryService, BrandQueryService>();
        return services;
    }
}
