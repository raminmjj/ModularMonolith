using ModularMonolith.Framework.Results;
using ModularMonolith.Contracts.Catalog;

namespace ModularMonolith.Modules.Catalog.Application.Ports.Inbound;

/// <summary>
/// Inbound port — the use case interface that the Application exposes to
/// inbound adapters (REST, gRPC, etc.). This IS the hexagon boundary.
/// Adapters call these methods; they never touch Service or Domain directly.
/// </summary>
public interface IProductService
{
    Task<Result<ProductSnapshot>> GetProductAsync(Guid productId, CancellationToken ct = default);
    Task<Result<ProductSnapshot>> CreateProductAsync(string sku, string name, decimal price, string currency, int initialStock, string? description, CancellationToken ct = default);
    Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default);
    Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default);
    Task<Result<ReservationSnapshot>> ReserveStockAsync(Guid productId, int quantity, TimeSpan ttl, CancellationToken ct = default);
    Task<Result> CommitReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default);
    Task<Result> ReleaseReservationAsync(Guid productId, Guid reservationId, CancellationToken ct = default);
    Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default);
}
