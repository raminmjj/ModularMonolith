using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Payment.Adapter.Outbound;
using ModularMonolith.Modules.Payment.Application;
using ModularMonolith.Modules.Payment.QueryApplication;

namespace ModularMonolith.Modules.Payment;

public static class ModuleInstaller
{
    public static IServiceCollection AddPaymentModule(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddPaymentApplication();             // IPaymentService → PaymentService (sealed)
        services.AddPaymentOutboundAdapters(config, isDevelopment); // DbContext, repos, IUnitOfWork, ICustomerGatewayPort
        services.AddPaymentRestAdapter();
        services.AddPaymentQueryApplication();
        return services;
    }
}
