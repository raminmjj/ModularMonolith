using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Dtos;
using ModularMonolith.Modules.Payment.Application.Ports.Inbound;
using ModularMonolith.Modules.Payment.QueryApplication;

namespace ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Endpoints;

public interface IEndpoint { void Map(IEndpointRouteBuilder app); }

public sealed class PaymentsEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments").WithTags("Payments");

        group.MapPost("/saved-card", async (IPaymentService svc, InitiateSavedCardPaymentRequest req, CancellationToken ct) =>
        {
            var r = await svc.InitiateWithSavedCardAsync(
                req.CustomerId, req.OrderId, req.Amount, req.Currency, req.SavedPaymentMethodId, ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/payments/{r.Value}", new PaymentCreatedResponse(r.Value, "Pending"))
                : ToError(r);
        }).RequireAuthorization();

        group.MapPost("/new-card-token", async (IPaymentService svc, InitiateNewCardTokenPaymentRequest req, CancellationToken ct) =>
        {
            var r = await svc.InitiateWithNewCardTokenAsync(
                req.CustomerId, req.OrderId, req.Amount, req.Currency, req.CardToken, req.CardType, req.ExpiryDate, ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/payments/{r.Value}", new PaymentCreatedResponse(r.Value, "Pending"))
                : ToError(r);
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (IPaymentQueryService query, Guid id, CancellationToken ct) =>
        {
            var r = await query.GetByIdAsync(id, ct);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound();
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/capture", async (IPaymentService svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.CaptureAsync(id, ct);
            return r.IsSuccess ? Results.NoContent() : ToError(r);
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/fail", async (IPaymentService svc, Guid id, FailPaymentRequest req, CancellationToken ct) =>
        {
            var r = await svc.FailAsync(id, req.Reason, ct);
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
    public static IServiceCollection AddPaymentRestAdapter(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<PaymentsEndpoints>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>().WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapPaymentsRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
