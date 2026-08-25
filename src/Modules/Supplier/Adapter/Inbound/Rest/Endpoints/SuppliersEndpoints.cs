using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Supplier.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Supplier.Adapter.Inbound.Rest.Endpoints;

public interface IEndpoint { void Map(IEndpointRouteBuilder app); }

public sealed class SuppliersEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/suppliers").WithTags("Suppliers");

        group.MapPost("/", async (ISupplierService svc, CreateSupplierRequest req, CancellationToken ct) =>
        {
            var r = await svc.CreateAsync(req.Name, req.ContactEmail, req.PhoneNumber, req.Address, ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/suppliers/{r.Value}", new { id = r.Value })
                : ToError(r);
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (ISupplierService svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.GetAsync(id, ct);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound();
        }).RequireAuthorization();

        // Admin operations are exposed via the Admin GraphQL endpoint (ADR-0007).
    }

    private static IResult ToError<T>(Result<T> r) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error.Code, Detail = r.Error.Message });
}

public sealed record CreateSupplierRequest(string Name, string ContactEmail, string? PhoneNumber, string? Address);

public static class DependencyInjection
{
    public static IServiceCollection AddSupplierRestAdapter(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<SuppliersEndpoints>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>().WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapSupplierRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
