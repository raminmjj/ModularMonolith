using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.QueryApplication;
using ModularMonolith.Modules.Customer.QueryApplication;
using ModularMonolith.Modules.Orders.QueryApplication;
using ModularMonolith.Modules.Payment.QueryApplication;
using ModularMonolith.Modules.Reporting.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Reporting.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingOutboundAdapters(this IServiceCollection services)
    {
        // Read-side ACL gateways (sanctioned exception #3 — see ADR-0006).
        services.AddScoped<Application.Ports.Outbound.IPaymentReadDataProvider, PaymentReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.ICustomerReadDataProvider, CustomerReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.IOrderReadDataProvider, OrdersReadDataProvider>();

        // Admin panel read providers (ADR-0007).
        services.AddScoped<Application.Ports.Outbound.IProductReadDataProvider, ProductReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.IPaymentAdminReadProvider, PaymentReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.ICustomerAdminReadProvider, CustomerReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.IOrderReadDataProvider, OrdersReadDataProvider>();

        // Write-side ACL gateways (ADR-0007) — delegate to provider inbound ports.
        services.AddScoped<Application.Ports.Outbound.ICatalogAdminProvider, CatalogAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.ICustomerAdminProvider, CustomerAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.IOrdersAdminProvider, OrdersAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.IPaymentAdminProvider, PaymentAdminGateway>();
        return services;
    }
}
