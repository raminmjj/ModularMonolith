using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Orders.Adapter.Outbound.Gateways;
using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Orders.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddModuleDbContext<OrdersDbContext>(config, "Orders", "Orders", "__EFMigrationsHistory_Orders", isDevelopment);
        services.AddScoped<Application.Ports.Outbound.IOrderRepository, OrderRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<Application.Ports.Outbound.ICatalogGateway, CatalogGateway>();
        return services;
    }
}
