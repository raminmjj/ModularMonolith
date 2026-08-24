using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Catalog;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Domain.Products;

using ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;
namespace ModularMonolith.Modules.Catalog.QueryApplication;

/// <summary>
/// Read-side query service. Deliberately OUTSIDE the hexagon — no aggregate
/// loading, no domain rules, no ports. Just direct EF Core projections for
/// maximum performance. CQRS separation: write side = Hexagonal, read side = flat.
/// </summary>
public interface IProductQueryService
{
    Task<Result<ProductSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSnapshot>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}

internal sealed class ProductQueryService : IProductQueryService
{
    private readonly DbContext _db;

    public ProductQueryService(CatalogDbContext db) => _db = db;

    public async Task<Result<ProductSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _db.Set<Product>()
            .Where(p => p.Id == id)
            .Select(p => new ProductSnapshot(p.Id, p.Name, p.Price.Amount, p.Stock - p.ReservedStock))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<ProductSnapshot>(new Error("PRODUCT_NOT_FOUND", "Product was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<ProductSnapshot>> ListAsync(int page, int pageSize, CancellationToken ct = default)
        => await _db.Set<Product>()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductSnapshot(p.Id, p.Name, p.Price.Amount, p.Stock - p.ReservedStock))
            .ToListAsync(ct);
}
