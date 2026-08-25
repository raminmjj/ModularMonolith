using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Brand.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Brand.Adapter.Outbound;
using ModularMonolith.Modules.Brand.Application;
using ModularMonolith.Modules.Brand.QueryApplication;

namespace ModularMonolith.Modules.Brand;

public static class ModuleInstaller
{
    public static IServiceCollection AddBrandModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddBrandApplication();
        services.AddBrandOutboundAdapters(config, isDevelopment);
        services.AddBrandRestAdapter();
        services.AddBrandQueryApplication();
        return services;
    }
}
