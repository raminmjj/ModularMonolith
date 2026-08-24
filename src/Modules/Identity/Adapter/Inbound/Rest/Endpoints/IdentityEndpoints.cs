using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Dtos;
using ModularMonolith.Modules.Identity.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Endpoints;

public sealed class IdentityEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity").WithTags("Identity");

        group.MapPost("/register", async (IUserService svc, RegisterRequest req, CancellationToken ct) =>
        {
            var r = await svc.RegisterAsync(req.Email, req.Password, req.DisplayName, ct);
            return r.IsSuccess ? Results.Ok(new { r.Value!.Id, r.Value.Email, r.Value.DisplayName }) : ToError(r);
        });

        group.MapPost("/login", async (IUserService svc, LoginRequest req, CancellationToken ct) =>
        {
            var r = await svc.LoginAsync(req.Email, req.Password, ct);
            return r.IsSuccess
                ? Results.Ok(new LoginResponse(r.Value!.AccessToken, r.Value.RefreshToken, r.Value.ExpiresAt, r.Value.User.Id, r.Value.User.Email, r.Value.User.DisplayName))
                : Results.Unauthorized();
        });

        group.MapPost("/refresh", async (IUserService svc, RefreshRequest req, CancellationToken ct) =>
        {
            var r = await svc.RefreshTokenAsync(req.RefreshToken, ct);
            return r.IsSuccess
                ? Results.Ok(new LoginResponse(r.Value!.AccessToken, r.Value.RefreshToken, r.Value.ExpiresAt, r.Value.User.Id, r.Value.User.Email, r.Value.User.DisplayName))
                : Results.Unauthorized();
        });

        group.MapGet("/users/{id:guid}", async (IUserService svc, Guid id, CancellationToken ct) =>
        {
            var r = await svc.GetByIdAsync(id, ct);
            return r.IsSuccess
                ? Results.Ok(new UserProfileResponse(r.Value!.Id, r.Value.Email, r.Value.DisplayName, r.Value.IsActive))
                : Results.NotFound();
        }).RequireAuthorization("Admin");

        group.MapGet("/me", (HttpContext ctx) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            return Results.Ok(new
            {
                id = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                email = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                roles = ctx.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray(),
            });
        }).RequireAuthorization();
    }

    private static IResult ToError<T>(Result<T> r) =>
        Results.BadRequest(new ProblemDetails { Status = 400, Title = r.Error.Code, Detail = r.Error.Message });
}

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityRestAdapter(this IServiceCollection services)
    {
        services.Scan(s => s.FromAssemblyOf<IdentityEndpoints>()
            .AddClasses(c => c.AssignableTo<IEndpoint>())
            .As<IEndpoint>().WithSingletonLifetime());
        return services;
    }

    public static IApplicationBuilder MapIdentityRestEndpoints(this WebApplication app)
    {
        foreach (var ep in app.Services.GetServices<IEndpoint>()) ep.Map(app);
        return app;
    }
}
