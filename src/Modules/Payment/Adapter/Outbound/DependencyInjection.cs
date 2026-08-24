using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Payment.Adapter.Outbound.Gateways;
using ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddModuleDbContext<PaymentDbContext>(config, "Payment", "Payment", "__EFMigrationsHistory_Payment", isDevelopment);
        services.AddScoped<Application.Ports.Outbound.IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, PaymentUnitOfWork>();
        // ★ The ACL gateway: ICustomerGatewayPort → Customer's ICustomerInboundPort (direct call).
        services.AddScoped<Application.Ports.Outbound.ICustomerGatewayPort, Gateways.CustomerGateway>();
        return services;
    }
}
