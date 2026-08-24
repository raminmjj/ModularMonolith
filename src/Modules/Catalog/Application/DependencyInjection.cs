using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.Application.Validation;

namespace ModularMonolith.Modules.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.IProductService, Service.ProductService>();

        // Register FluentValidation validators — invoked at hexagon boundary
        services.AddScoped<CreateProductValidator>();
        services.AddScoped<ChangePriceValidator>();
        services.AddScoped<ReserveStockValidator>();

        return services;
    }
}
