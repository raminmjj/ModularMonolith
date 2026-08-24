using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Framework.Common;
using ModularMonolith.Modules.Identity.Application.Validation;

namespace ModularMonolith.Modules.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<Ports.Inbound.IUserService, Service.UserService>();
        services.AddSingleton<IClock, SystemClock>();

        // Register FluentValidation validators — invoked at hexagon boundary
        services.AddScoped<RegisterValidator>();
        services.AddScoped<LoginValidator>();

        return services;
    }
}
