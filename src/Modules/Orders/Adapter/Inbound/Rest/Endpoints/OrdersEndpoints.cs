using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Orders.Adapter.Inbound.Rest.Dtos;
using ModularMonolith.Modules.Orders.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Orders.Adapter.Inbound.Rest.Endpoints;

public interface IEndpoint { void Map(IEndpointRouteBuilder app); }

public sealed class OrdersEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders").WithTags("Orders");

        group.MapPost("/", async (IOrderService svc, PlaceOrderRequest req, CancellationToken ct) =>
        {
            var r = await svc.PlaceOrderAsync(req.UserId, req.Lines.Select(l => (l.ProductId, l.Quantity)), ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/orders/{r.Value!.Id}",
                    new OrderResponse(r.Value.Id, r.Value.UserId, r.Value.PlacedOn, r.Value.Status, r.Value.TotalAmount))
                : ToError(r);
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (IOrderService svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.GetByIdAsync(id, ct);
            return r.IsSuccess
                ? Results.Ok(new OrderResponse(r.Value!.Id, r.Value.UserId, r.Value.PlacedOn, r.Value.Status, r.Value.TotalAmount))
                : Results.NotFound();
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/status", async (IOrderService svc, Guid id, string status, CancellationToken ct) =>
        {
            var r = await svc.ChangeStatusAsync(id, status, ct);
            return r.IsSuccess ? Results.NoContent() : ToError(r);
        }).RequireAuthorization();
    }

    private static IResult ToError<T>(Result<T> r) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error.Code, Detail = r.Error.Message });
    private static IResult ToError(Result r) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error.Code, Detail = r.Error.Message });
}

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersRestAdapter(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<OrdersEndpoints>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>().WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapOrdersRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
