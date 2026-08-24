using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Customer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.ICustomerInboundPort, Service.CustomerService>();
        services.AddScoped<Ports.Inbound.ICustomerAdminService, Service.CustomerAdminService>();
        return services;
    }
}
