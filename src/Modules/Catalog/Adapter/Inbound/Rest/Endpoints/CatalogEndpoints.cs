using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Dtos;
using ModularMonolith.Modules.Catalog.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Endpoints;

/// <summary>
/// REST driving adapter for Catalog. Maps HTTP requests to IProductService
/// (the inbound port). This adapter knows about HTTP; the hexagon does not.
/// </summary>
public sealed class CatalogEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/catalog").WithTags("Catalog");

        group.MapPost("/products", async (IProductService svc, CreateProductRequest req, CancellationToken ct) =>
        {
            var result = await svc.CreateProductAsync(req.Sku, req.Name, req.Price, req.Currency, req.InitialStock, req.Description, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/catalog/products/{result.Value!.Id}", result.Value)
                : ToError(result);
        }).RequireAuthorization("Admin");

        group.MapPost("/products/{id:guid}/price", async (IProductService svc, Guid id, ChangePriceRequest req, CancellationToken ct) =>
        {
            if (id != req.ProductId) return Results.BadRequest("Mismatched product id.");
            var result = await svc.ChangePriceAsync(id, req.NewPrice, req.Currency, ct);
            return result.IsSuccess ? Results.NoContent() : ToError(result);
        }).RequireAuthorization("Admin");

        group.MapPost("/products/{id:guid}/stock", async (IProductService svc, Guid id, AdjustStockRequest req, CancellationToken ct) =>
        {
            if (id != req.ProductId) return Results.BadRequest("Mismatched product id.");
            var result = await svc.AdjustStockAsync(id, req.Delta, ct);
            return result.IsSuccess ? Results.NoContent() : ToError(result);
        }).RequireAuthorization("Admin");

        group.MapPost("/products/{id:guid}/reserve", async (IProductService svc, Guid id, ReserveStockRequest req, CancellationToken ct) =>
        {
            var ttl = req.TtlMinutes > 0 ? TimeSpan.FromMinutes(req.TtlMinutes) : TimeSpan.FromMinutes(10);
            var result = await svc.ReserveStockAsync(id, req.Quantity, ttl, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ToError(result);
        });

        group.MapPost("/products/{id:guid}/reserve/{reservationId:guid}/commit", async (IProductService svc, Guid id, Guid reservationId, CancellationToken ct) =>
        {
            var result = await svc.CommitReservationAsync(id, reservationId, ct);
            return result.IsSuccess ? Results.NoContent() : ToError(result);
        });

        group.MapPost("/products/{id:guid}/reserve/{reservationId:guid}/release", async (IProductService svc, Guid id, Guid reservationId, CancellationToken ct) =>
        {
            var result = await svc.ReleaseReservationAsync(id, reservationId, ct);
            return result.IsSuccess ? Results.NoContent() : ToError(result);
        });
    }

    private static IResult ToError(Result result) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = result.Error.Code, Detail = result.Error.Message });

    private static IResult ToError<T>(Result<T> result) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = result.Error.Code, Detail = result.Error.Message });
}
