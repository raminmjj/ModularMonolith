using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Supplier.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddSupplierQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<ISupplierQueryService, SupplierQueryService>();
        return services;
    }
}
