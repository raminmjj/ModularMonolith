using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Supplier.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSupplierApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.ISupplierService, Service.SupplierService>();
        services.AddScoped<Ports.Inbound.ISupplierAdminService, Service.SupplierAdminService>();
        return services;
    }
}
