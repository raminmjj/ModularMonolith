using ModularMonolith.Contracts.Catalog;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Ports.Inbound;
using ModularMonolith.Modules.Orders.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Orders.Adapter.Outbound.Gateways;

/// <summary>
/// ACL adapter — implements Orders' ICatalogGateway outbound port
/// by delegating to Catalog's IProductService inbound port.
///
/// This is a DIRECT METHOD CALL, not a message bus publish. It is NOT a shared
/// database transaction: Catalog and Orders use separate databases. Partial
/// failures are compensated by the caller (CrossModuleSaga) — e.g. reservations
/// are released if the order cannot be placed. No eventual consistency, no
/// Wolverine, no events. See docs/adr/0005.
/// </summary>
internal sealed class CatalogGateway : ICatalogGateway
{
    private readonly IProductService _catalogService;

    public CatalogGateway(IProductService catalogService) => _catalogService = catalogService;

    public async Task<Result<ProductSnapshot>> GetProductAsync(Guid productId, CancellationToken ct = default)
        => await _catalogService.GetProductAsync(productId, ct);

    public async Task<Result<ReservationSnapshot>> ReserveStockAsync(Guid productId, int quantity, TimeSpan ttl, CancellationToken ct = default)
        => await _catalogService.ReserveStockAsync(productId, quantity, ttl, ct);

    public async Task<Result> CommitReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default)
        => await _catalogService.CommitReservationAsync(productId, reservationId, ct);

    public async Task<Result> ReleaseReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default)
        => await _catalogService.ReleaseReservationAsync(productId, reservationId, ct);
}
