using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Brand.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Brand.Adapter.Inbound.Rest.Endpoints;

public interface IEndpoint { void Map(IEndpointRouteBuilder app); }

public sealed class BrandsEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/brands").WithTags("Brands");

        group.MapPost("/", async (IBrandService svc, CreateBrandRequest req, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(req.Name, req.Slug, req.Description, req.LogoUrl, req.CountryOfOrigin, ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/brands/{r.Value}", new { id = r.Value })
                : Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error!.Code, Detail = r.Error.Message });
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (IBrandService svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(id, ct);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound();
        }).RequireAuthorization();
    }
}

public sealed record CreateBrandRequest(string Name, string? Slug, string? Description, string? LogoUrl, string CountryOfOrigin);

public static class DependencyInjection
{
    public static IServiceCollection AddBrandRestAdapter(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<BrandsEndpoints>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>().WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapBrandRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
