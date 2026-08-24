using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Customer.QueryApplication;
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
        return services;
    }
}
