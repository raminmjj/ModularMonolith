using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Identity.QueryApplication;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityQueryApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserQueryService, UserQueryService>();
        return services;
    }
}
