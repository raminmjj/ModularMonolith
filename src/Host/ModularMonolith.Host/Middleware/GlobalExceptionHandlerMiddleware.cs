using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModularMonolith.DDD.Common;

namespace ModularMonolith.Host.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (DomainException dex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Domain exception: {Code} - {Message}", dex.Code, dex.Message);
            await WriteAsync(ctx, HttpStatusCode.BadRequest, dex.Code, dex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Concurrency conflict while saving aggregate");
            await WriteAsync(ctx, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT",
                "The resource was modified by another transaction. Please retry.");
        }
        catch (FluentValidation.ValidationException vex)
        {
            await WriteAsync(ctx, HttpStatusCode.BadRequest, "VALIDATION",
                JsonSerializer.Serialize(vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(ctx, HttpStatusCode.InternalServerError, "INTERNAL", "An unexpected error occurred.");
        }
    }

    private static async Task WriteAsync(HttpContext ctx, HttpStatusCode status, string code, string detail)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = code, message = detail }));
    }
}
