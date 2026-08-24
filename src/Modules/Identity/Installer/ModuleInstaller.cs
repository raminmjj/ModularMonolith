using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Identity.Adapter.Outbound;
using ModularMonolith.Modules.Identity.Application;
using ModularMonolith.Modules.Identity.QueryApplication;

namespace ModularMonolith.Modules.Identity;

public static class ModuleInstaller
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddIdentityApplication();
        services.AddIdentityOutboundAdapters(config, isDevelopment);
        services.AddIdentityRestAdapter();
        services.AddIdentityQueryApplication();
        return services;
    }
}
