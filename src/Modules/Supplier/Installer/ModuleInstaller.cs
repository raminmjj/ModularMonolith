using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Supplier.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Supplier.Adapter.Outbound;
using ModularMonolith.Modules.Supplier.Application;
using ModularMonolith.Modules.Supplier.QueryApplication;

namespace ModularMonolith.Modules.Supplier;

public static class ModuleInstaller
{
    public static IServiceCollection AddSupplierModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddSupplierApplication();
        services.AddSupplierOutboundAdapters(config, isDevelopment);
        services.AddSupplierRestAdapter();
        services.AddSupplierQueryApplication();
        return services;
    }
}
