using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Ports.Inbound;
using ModularMonolith.Modules.Catalog.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Catalog.Application.Service;

/// <summary>
/// Admin facade over the Catalog context (ADR-0007). Most operations delegate to
/// the public service; DeactivateProductAsync is MATERIALIZED because publishing
/// the aggregate&apos;s ProductDeactivatedDomainEvent is an admin-path concern —
/// it needs the loaded aggregate, the IEventDispatcher seam (ADR-0004), and the
/// unit of work. Same domain rules, same transactional semantics.
/// </summary>
public sealed class CatalogAdminService(
    IProductService products,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IEventDispatcher eventDispatcher) : Ports.Inbound.ICatalogAdminService
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

    /// <summary>
    /// Deactivates the product and publishes <see cref="ProductDeactivatedDomainEvent"/>
    /// via the pluggable dispatcher (NoOp today — ADR-0004). When a real dispatcher
    /// replaces NoOp, side effects (cache invalidation, notifications) start flowing
    /// with ZERO changes here.
    /// </summary>
    public async Task<Result> DeactivateProductAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result.Failure(new Error("PRODUCT_NOT_FOUND", "Product was not found."));

        try { product.Deactivate(); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        await unitOfWork.SaveChangesAsync(ct);

        // Publish what the aggregate raised (NoOp today), then clear its buffer.
        await eventDispatcher.DispatchAndClearAsync(product, ct);
        return Result.Success();
    }
}
