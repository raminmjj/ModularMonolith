using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Reporting.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Reporting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingApplication(this IServiceCollection services)
    {
        services.AddMemoryCache(); // report result cache (30s TTL)
        services.AddScoped<Ports.Inbound.IFailureReportService, Service.FailureReportService>();
        return services;
    }
}
