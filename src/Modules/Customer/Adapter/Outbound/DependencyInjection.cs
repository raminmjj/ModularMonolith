using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Customer.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddModuleDbContext<CustomerDbContext>(config, "Customer", "Customer", "__EFMigrationsHistory_Customer", isDevelopment);
        services.AddScoped<Application.Ports.Outbound.ICustomerRepository, CustomerRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, CustomerUnitOfWork>();
        return services;
    }
}
