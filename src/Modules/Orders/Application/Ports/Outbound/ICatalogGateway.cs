using ModularMonolith.Contracts.Catalog;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Orders.Application.Ports.Outbound;

/// <summary>
/// ACL port — Orders calls Catalog synchronously within one logical operation.
/// IMPORTANT: this is NOT a shared database transaction (separate DBs). Failures
/// after partial completion are handled via compensation — see CrossModuleSaga
/// and docs/adr/0005. NO events, NO message bus.
///
/// The implementation (CatalogGateway) lives in Adapter/Outbound/Gateways
/// and delegates to Catalog's IProductService (inbound port).
/// </summary>
public interface ICatalogGateway
{
    Task<Result<ProductSnapshot>> GetProductAsync(Guid productId, CancellationToken ct = default);
    Task<Result<ReservationSnapshot>> ReserveStockAsync(Guid productId, int quantity, TimeSpan ttl, CancellationToken ct = default);
    Task<Result> CommitReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default);
    Task<Result> ReleaseReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default);
}
