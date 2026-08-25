using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Brand.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Brand.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddBrandOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.AddDbContext<BrandDbContext>(opts =>
        {
            var cs = config.GetConnectionString("Brand");
            if (!string.IsNullOrWhiteSpace(cs))
                opts.UseSqlServer(cs, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Brand"));
            else if (isDevelopment)
                opts.UseInMemoryDatabase("Brand");
            else
                throw new InvalidOperationException(
                    "Connection string 'Brand' is not configured and the environment is not Development.");
        });
        services.AddScoped<Application.Ports.Outbound.IBrandRepository, BrandRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, BrandUnitOfWork>();
        return services;
    }
}
