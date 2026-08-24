using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Catalog.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductQueryService, ProductQueryService>();
        return services;
    }
}
