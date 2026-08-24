using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Orders.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.IOrderService, Service.OrderService>();
        return services;
    }
}
