using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Payment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.IPaymentService, Service.PaymentService>();
        services.AddScoped<Ports.Inbound.IPaymentAdminService, Service.PaymentAdminService>();
        return services;
    }
}
