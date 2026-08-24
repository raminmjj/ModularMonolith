using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;using ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Catalog.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddModuleDbContext<CatalogDbContext>(config, "Catalog", "Catalog", "__EFMigrationsHistory_Catalog", isDevelopment);
        services.AddScoped<Application.Ports.Outbound.IProductRepository, ProductRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, CatalogUnitOfWork>();
        services.AddHostedService<ReservationExpirationService>();
        return services;
    }
}
