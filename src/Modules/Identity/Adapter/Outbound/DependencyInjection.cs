using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Security;

namespace ModularMonolith.Modules.Identity.Adapter.Outbound;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityOutboundAdapters(this IServiceCollection services, IConfiguration config, bool isDevelopment)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddModuleDbContext<IdentityDbContext>(config, "Identity", "Identity", "__EFMigrationsHistory_Identity", isDevelopment);
        // Register the typed DbContext as base DbContext so QueryApplication can inject it.
        services.AddScoped<Application.Ports.Outbound.IUserRepository, UserRepository>();
        services.AddScoped<Application.Ports.Outbound.IUnitOfWork, IdentityUnitOfWork>();
        services.AddSingleton<Application.Domain.Users.IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<Application.Ports.Outbound.ITokenService, JwtTokenService>();
        return services;
    }
}
