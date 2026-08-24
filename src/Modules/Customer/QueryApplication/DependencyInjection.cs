using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Customer.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        return services;
    }
}
