using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Reporting.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Reporting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingApplication(this IServiceCollection services)
    {
        services.AddMemoryCache(); // report + analytics result cache (30s TTL)
        services.AddScoped<Ports.Inbound.IFailureReportService, Service.FailureReportService>();
        services.AddScoped<Ports.Inbound.IAdminCatalogService, Service.AdminCatalogService>();
        services.AddScoped<Ports.Inbound.IAdminCustomerService, Service.AdminCustomerService>();
        services.AddScoped<Ports.Inbound.IAdminOrderService, Service.AdminOrderService>();
        services.AddScoped<Ports.Inbound.IAdminPaymentService, Service.AdminPaymentService>();
        services.AddScoped<Ports.Inbound.IAdminAnalyticsService, Service.AdminAnalyticsService>();
        return services;
    }
}
