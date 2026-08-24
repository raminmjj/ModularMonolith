using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Catalog.Application.Service;

/// <summary>
/// Admin facade over the Catalog public service. Same module, same domain rules —
/// delegation is deliberate (zero duplication); admin-only rules belong HERE when
/// they appear, never in the Admin module.
/// </summary>
public sealed class CatalogAdminService(IProductService products) : Ports.Inbound.ICatalogAdminService
{
    public async Task<Result<Guid>> CreateProductAsync(string sku, string name, decimal price, string currency,
        int initialStock, string? description, CancellationToken ct = default)
    {
        var result = await products.CreateProductAsync(sku, name, price, currency, initialStock, description, ct);
        return result.IsSuccess ? Result.Success(result.Value!.Id) : Result.Failure<Guid>(result.Error);
    }

    public Task<Result> ChangePriceAsync(Guid productId, decimal newPrice, string currency, CancellationToken ct = default)
        => products.ChangePriceAsync(productId, newPrice, currency, ct);

    public Task<Result> AdjustStockAsync(Guid productId, int delta, CancellationToken ct = default)
        => products.AdjustStockAsync(productId, delta, ct);

    public Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default)
        => products.DeactivateProductAsync(productId, ct);
}
