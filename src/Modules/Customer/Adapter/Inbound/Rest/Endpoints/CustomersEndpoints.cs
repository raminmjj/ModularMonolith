using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Dtos;
using ModularMonolith.Modules.Customer.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Endpoints;

public interface IEndpoint { void Map(IEndpointRouteBuilder app); }

public sealed class CustomersEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/customers").WithTags("Customers");

        group.MapPost("/", async (ICustomerInboundPort svc, RegisterCustomerRequest req, CancellationToken ct) =>
        {
            var r = await svc.RegisterAsync(req.IdentityUserId, req.DisplayName, ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/customers/{r.Value}", new CustomerCreatedResponse(r.Value))
                : ToError(r);
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (ICustomerInboundPort svc, Guid id, CancellationToken ct) =>
        {
            var status = await svc.GetStatusAsync(id, ct);
            return status.IsSuccess
                ? Results.Ok(status.Value)
                : Results.NotFound();
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/addresses", async (ICustomerInboundPort svc, Guid id, AddAddressRequest req, CancellationToken ct) =>
        {
            var r = await svc.AddAddressAsync(id, req.Street, req.City, req.PostalCode, req.Country, ct);
            return r.IsSuccess ? Results.NoContent() : ToError(r);
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/payment-methods", async (ICustomerInboundPort svc, Guid id, SavePaymentMethodRequest req, CancellationToken ct) =>
        {
            var r = await svc.SavePaymentMethodAsync(id, req.TokenizedCard, req.CardType, req.ExpiryDate, ct);
            return r.IsSuccess
                ? Results.Created($"/api/v1/customers/{id}", new CustomerCreatedResponse(r.Value))
                : ToError(r);
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/suspend", async (ICustomerInboundPort svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.SuspendAsync(id, ct);
            return r.IsSuccess ? Results.NoContent() : ToError(r);
        }).RequireAuthorization("Admin");

        group.MapPost("/{id:guid}/reactivate", async (ICustomerInboundPort svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.ReactivateAsync(id, ct);
            return r.IsSuccess ? Results.NoContent() : ToError(r);
        }).RequireAuthorization("Admin");
    }

    private static IResult ToError<T>(Result<T> r) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error.Code, Detail = r.Error.Message });
    private static IResult ToError(Result r) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error.Code, Detail = r.Error.Message });
}

public static class DependencyInjection
{
    public static IServiceCollection AddCustomerRestAdapter(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<CustomersEndpoints>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>().WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapCustomerRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
