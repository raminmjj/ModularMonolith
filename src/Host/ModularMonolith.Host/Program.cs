using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ModularMonolith.Host.Middleware;
using ModularMonolith.Modules.Catalog;
using ModularMonolith.Modules.Customer;
using ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Customer.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Payment;
using ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Admin;
using ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;
using ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Identity;
using ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Security;
using ModularMonolith.Modules.Orders;
using ModularMonolith.Modules.Orders.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Serilog ----
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
        formatProvider: System.Globalization.CultureInfo.InvariantCulture));

// ---- Modular composition root — Host is intentionally thin ----
// Each module registers its own Application + Adapters + QueryApplication.
// No Wolverine, no message bus — all cross-module calls are transactional
// direct method calls through hexagonal ports.
builder.Services
    .AddIdentityModule(builder.Configuration, builder.Environment.IsDevelopment())
    .AddCatalogModule(builder.Configuration, builder.Environment.IsDevelopment())
    .AddOrdersModule(builder.Configuration, builder.Environment.IsDevelopment())
    .AddCustomerModule(builder.Configuration, builder.Environment.IsDevelopment()) // provider BEFORE consumer
    .AddPaymentModule(builder.Configuration, builder.Environment.IsDevelopment()) // consumer resolves ICustomerInboundPort
    .AddAdminModule(builder.Configuration); // admin composition: reads + writes via ACL gateways (ADR-0007)

// ---- JWT Auth ----
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
jwt.SigningKey = Environment.GetEnvironmentVariable("JWT__SIGNINGKEY")
    ?? builder.Configuration["Jwt:SigningKey"]
    ?? string.Empty;

if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        jwt.SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("WARNING: JWT__SIGNINGKEY env var not set. Using ephemeral dev key. DO NOT use in production.");
        Console.ResetColor();
    }
    else
    {
        throw new InvalidOperationException("JWT__SIGNINGKEY environment variable must be set (min 32 chars) in production.");
    }
}

// Propagate the RESOLVED signing key (env var or ephemeral dev key) back into the
// bound options so JwtTokenService signs with the same key JwtBearer validates with.
// Without this, the ephemeral dev key existed only in the local variable above.
builder.Services.PostConfigure<JwtOptions>(opts => opts.SigningKey = jwt.SigningKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("Admin", p => p.RequireRole("Admin"));
    opts.AddPolicy("Customer", p => p.RequireRole("Customer", "Admin"));
});

// ---- CORS ----
var allowedOrigins = builder.Configuration.GetSection("Host:AllowedCorsOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("default", p =>
    {
        p.AllowAnyHeader().AllowAnyMethod();
        if (allowedOrigins.Length > 0) p.WithOrigins(allowedOrigins).AllowCredentials();
        else p.AllowAnyOrigin();
    });
});

// ---- Rate Limiting ----
builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 100, Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst, QueueLimit = 0
            }));
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Named policy for expensive admin reports (GraphQL).
    opts.AddPolicy("reports", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// ---- OpenAPI + Scalar ----
builder.Services.AddOpenApi();

// ---- Health Checks ----
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("Identity DB")
    .AddDbContextCheck<CatalogDbContext>("Catalog DB")
    .AddDbContextCheck<OrdersDbContext>("Orders DB")
    .AddDbContextCheck<CustomerDbContext>("Customer DB")
    .AddDbContextCheck<PaymentDbContext>("Payment DB");

// ---- OpenTelemetry ----
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ModularMonolith"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("ModularMonolith"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddMeter("ModularMonolith.Sagas"));

// ---- Pluggable event dispatching (ADR-0004, currently NoOp) ----
// Application services await IEventDispatcher; today nothing is delivered.
// To go async later: register a different IEventDispatcher here — the core
// does not change. NO bus packages; see ADR-0004 for future triggers.
builder.Services.AddSingleton<ModularMonolith.DDD.Events.IEventDispatcher>(
    ModularMonolith.DDD.Events.NoOpEventDispatcher.Instance);

var app = builder.Build();

// ---- Explicit schema management: `dotnet ModularMonolith.Host.dll --migrate` ----
// The host NEVER auto-migrates on startup (production safety: replicas, rollbacks,
// and DBA-controlled releases). Apply migrations deliberately — via this flag
// (docker-compose runs it as an init container) or a CI/CD job.
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    DbContext[] contexts =
    [
        scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
        scope.ServiceProvider.GetRequiredService<CatalogDbContext>(),
        scope.ServiceProvider.GetRequiredService<OrdersDbContext>(),
        scope.ServiceProvider.GetRequiredService<CustomerDbContext>(),
        scope.ServiceProvider.GetRequiredService<PaymentDbContext>(),
    ];

    foreach (var db in contexts)
    {
        if (db.Database.IsInMemory())
        {
            Console.WriteLine($"[migrate] {db.GetType().Name}: skipped ({db.Database.ProviderName} is non-relational).");
            continue;
        }
        await db.Database.MigrateAsync();
        Console.WriteLine($"[migrate] {db.GetType().Name}: migrations applied.");
    }
    Console.WriteLine("[migrate] Done.");
    return;
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "ModularMonolith API";
        options.Theme = ScalarTheme.Purple;
        options.HideModels = false;
        options.ShowSidebar = true;
    });
}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors("default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ---- Map all module endpoints ----
app.MapIdentityRestEndpoints();
app.MapRestEndpoints(); // Catalog
app.MapOrdersRestEndpoints();
app.MapCustomerRestEndpoints();
app.MapPaymentsRestEndpoints();
app.MapAdminEndpoints();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
