using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Endpoints;

public static class EndpointCompositionExtensions
{
    public static IServiceCollection AddRestEndpoints<TMarker>(this IServiceCollection services) where TMarker : class
    {
        services.Scan(s => s.FromAssemblyOf<TMarker>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>()
            .WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
