using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Payment.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<IPaymentQueryService, PaymentQueryService>();
        return services;
    }
}
