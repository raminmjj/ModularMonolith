using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest;
using ModularMonolith.Modules.Catalog.Adapter.Outbound;
using ModularMonolith.Modules.Catalog.Application;
using ModularMonolith.Modules.Catalog.QueryApplication;

namespace ModularMonolith.Modules.Catalog;

public static class ModuleInstaller
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddCatalogApplication();
        services.AddCatalogOutboundAdapters(config, isDevelopment);
        services.AddCatalogRestAdapter();
        services.AddCatalogQueryApplication();
        return services;
    }
}
