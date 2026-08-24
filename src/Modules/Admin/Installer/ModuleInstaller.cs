using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;
using ModularMonolith.Modules.Admin.Adapter.Outbound;
using ModularMonolith.Modules.Admin.Application;

namespace ModularMonolith.Modules.Admin;

/// <summary>
/// The module is a pure COMPOSITION context: no own DbContext, no QueryApplication
/// of its own (its read side IS the composition over provider read sides).
/// Hence 4 projects, not 5 — documented in ADR-0006.
/// </summary>
public static class ModuleInstaller
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddAdminApplication();        // IFailureReportService (composition + cache + validation)
        services.AddAdminOutboundAdapters();   // read-side ACL gateways → Customer/Payment QueryApplications
        services.AddAdminGraphQL();            // HotChocolate server "admin" + resolvers
        return services;
    }
}
