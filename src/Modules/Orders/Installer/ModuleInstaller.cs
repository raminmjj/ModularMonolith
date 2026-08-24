using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Orders.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Orders.Adapter.Outbound;
using ModularMonolith.Modules.Orders.Application;
using ModularMonolith.Modules.Orders.QueryApplication;

namespace ModularMonolith.Modules.Orders;

public static class ModuleInstaller
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddOrdersApplication();
        services.AddOrdersOutboundAdapters(config, isDevelopment);
        services.AddOrdersRestAdapter();
        services.AddOrdersQueryApplication();
        return services;
    }
}
