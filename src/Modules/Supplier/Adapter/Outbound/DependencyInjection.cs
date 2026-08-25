using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Supplier.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Supplier.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddSupplierOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddDbContext<SupplierDbContext>(opts =>
        {
            var cs = config.GetConnectionString("Supplier");
            if (!string.IsNullOrWhiteSpace(cs))
                opts.UseSqlServer(cs, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Supplier"));
            else if (isDevelopment)
                opts.UseInMemoryDatabase("Supplier");
            else
                throw new InvalidOperationException(
                    "Connection string 'Supplier' is not configured and the environment is not Development.");
        });
        services.AddScoped<Application.Ports.Outbound.ISupplierRepository, SupplierRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, SupplierUnitOfWork>();
        services.AddScoped<Application.Ports.Outbound.IBrandExistenceChecker, BrandExistenceChecker>();
        return services;
    }
}
