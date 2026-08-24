using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Endpoints;

namespace ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogRestAdapter(this IServiceCollection services)
    {
        services.AddRestEndpoints<CatalogEndpoints>();
        return services;
    }
}
