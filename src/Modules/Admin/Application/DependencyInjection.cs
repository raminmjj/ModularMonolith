using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Admin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddMemoryCache(); // report + analytics result cache (30s TTL)

        // COMPOSITION-only services (ADR-0007 amendment). Single-module admin
        // operations live in the domain modules' own admin ports and are reached
        // by GraphQL via the Admin gateway ports — never re-implemented here.
        services.AddScoped<Ports.Inbound.IFailureReportService, Service.FailureReportService>();
        services.AddScoped<Ports.Inbound.IAdminCustomerService, Service.AdminCustomerService>();
        services.AddScoped<Ports.Inbound.IAdminOrderService, Service.AdminOrderService>();
        services.AddScoped<Ports.Inbound.IAdminAnalyticsService, Service.AdminAnalyticsService>();
        return services;
    }
}
