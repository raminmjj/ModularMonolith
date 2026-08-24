using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ModularMonolith.Modules.Admin.Adapter.Inbound.GraphQL;

public static class DependencyInjection
{
    public static IServiceCollection AddAdminGraphQL(this IServiceCollection services)
    {
        services
            .AddGraphQLServer("admin")
            .AddQueryType(d => d.Name("Query"))
            .AddMutationType(d => d.Name("Mutation"))
            .AddTypeExtension<AdminCatalogQueries>()
            .AddTypeExtension<AdminCustomerQueries>()
            .AddTypeExtension<AdminOrderQueries>()
            .AddTypeExtension<AdminPaymentQueries>()
            .AddTypeExtension<AdminAnalyticsQueries>()
            .AddTypeExtension<AdminReportQueries>()   // failed-payments report (existing)
            .AddTypeExtension<AdminCatalogMutations>()
            .AddTypeExtension<AdminCustomerMutations>()
            .AddTypeExtension<AdminOrderMutations>()
            .AddTypeExtension<AdminPaymentMutations>()
            .AddAuthorization() // enables @authorize directive + [Authorize] on resolvers
            .AddMaxExecutionDepthRule(10); // guard against runaway nested queries

        return services;
    }

    /// <summary>
    /// Maps the admin GraphQL endpoint. Defense in depth: the ASP.NET authorization
    /// policy gates the ENDPOINT (blocks introspection for non-admins), and
    /// [Authorize(Policy = "Admin")] additionally gates the FIELD.
    /// Rate limiting via the named "reports" policy (configured in Host).
    /// </summary>
    public static IApplicationBuilder MapAdminEndpoints(this WebApplication app)
    {
        app.MapGraphQL("/api/v1/admin/reports/graphql")
            .RequireAuthorization("Admin")
            .RequireRateLimiting("reports");

        app.MapGraphQLSchema("/api/v1/admin/reports/graphql/schema.gql");

        return app;
    }
}
