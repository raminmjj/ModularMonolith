using Microsoft.AspNetCore.Routing;
namespace ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Endpoints;
public interface IEndpoint { void Map(IEndpointRouteBuilder app); }
