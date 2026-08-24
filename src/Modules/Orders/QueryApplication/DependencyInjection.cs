using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Orders.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        return services;
    }
}
