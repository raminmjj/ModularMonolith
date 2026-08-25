using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.QueryApplication;
using ModularMonolith.Modules.Customer.QueryApplication;
using ModularMonolith.Modules.Orders.QueryApplication;
using ModularMonolith.Modules.Payment.QueryApplication;

namespace ModularMonolith.Modules.Admin.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminOutboundAdapters(this IServiceCollection services)
    {
        // Read-side ACL gateways (sanctioned exception #3 — see ADR-0006).
        services.AddScoped<Application.Ports.Outbound.IPaymentReadDataProvider, PaymentReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.ICustomerReadDataProvider, CustomerReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.IOrderReadDataProvider, OrdersReadDataProvider>();

        // Admin panel read providers (ADR-0007).
        services.AddScoped<Application.Ports.Outbound.IProductReadDataProvider, ProductReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.IPaymentAdminReadProvider, PaymentReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.ICustomerAdminReadProvider, CustomerReadDataProvider>();

        // Write-side ACL gateways (ADR-0007 amendment) — delegate to the domain
        // modules' OWN admin ports (domain-owned operations).
        services.AddScoped<Application.Ports.Outbound.ICatalogAdminGateway, CatalogAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.ICustomerAdminGateway, CustomerAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.IOrdersAdminGateway, OrdersAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.IPaymentAdminGateway, PaymentAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.ISupplierAdminGateway, SuppliersAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.IBrandAdminGateway, BrandsAdminGateway>();
        services.AddScoped<Application.Ports.Outbound.ISupplierReadDataProvider, SuppliersReadDataProvider>();
        services.AddScoped<Application.Ports.Outbound.IBrandReadDataProvider, BrandsReadDataProvider>();
        return services;
    }
}
