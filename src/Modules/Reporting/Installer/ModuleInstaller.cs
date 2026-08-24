using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Reporting.Adapter.Inbound.GraphQL;
using ModularMonolith.Modules.Reporting.Adapter.Outbound;
using ModularMonolith.Modules.Reporting.Application;

namespace ModularMonolith.Modules.Reporting;

/// <summary>
/// Reporting is a pure COMPOSITION context: no own DbContext, no QueryApplication
/// of its own (its read side IS the composition over provider read sides).
/// Hence 4 projects, not 5 — documented in ADR-0006.
/// </summary>
public static class ModuleInstaller
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddReportingApplication();        // IFailureReportService (composition + cache + validation)
        services.AddReportingOutboundAdapters();   // read-side ACL gateways → Customer/Payment QueryApplications
        services.AddReportingGraphQL();            // HotChocolate server "admin" + resolvers
        return services;
    }
}
