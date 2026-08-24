using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Customer.Adapter.Outbound;
using ModularMonolith.Modules.Customer.Application;
using ModularMonolith.Modules.Customer.QueryApplication;

namespace ModularMonolith.Modules.Customer;

public static class ModuleInstaller
{
    public static IServiceCollection AddCustomerModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddCustomerApplication();            // ICustomerInboundPort → CustomerService (sealed)
        services.AddCustomerOutboundAdapters(config, isDevelopment); // DbContext, repos, IUnitOfWork
        services.AddCustomerRestAdapter();            // IEndpoint scan (Scrutor)
        services.AddCustomerQueryApplication();
        return services;
    }
}
