using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Catalog.Application.Ports.Inbound;

/// <summary>
/// Admin-owned operations for the Catalog context (ADR-0007 amendment).
/// The domain module owns its admin surface; the Admin module reaches it only
/// through this port. v1 delegates to the public product service — admin-specific
/// rules land here when they exist.
/// </summary>
public interface ICatalogAdminService
{
    Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency, int initialStock, string? description, CancellationToken ct = default);
    Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default);
    Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default);
    Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default);
}
