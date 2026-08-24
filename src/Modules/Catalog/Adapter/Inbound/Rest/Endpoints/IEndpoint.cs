using Microsoft.AspNetCore.Routing;

namespace ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Endpoints;

/// <summary>Shared marker for endpoint discovery.</summary>
public interface IEndpoint { void Map(IEndpointRouteBuilder app); }
