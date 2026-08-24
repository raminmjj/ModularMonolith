using Microsoft.EntityFrameworkCore;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories;

/// <summary>
/// Driven adapter — implements IProductRepository (outbound port) using EF Core.
/// The hexagon defined WHAT it needs; this adapter provides HOW (SQL Server).
/// </summary>
internal sealed class ProductRepository : EfCoreRepository<Application.Domain.Products.Product, Guid>, Application.Ports.Outbound.IProductRepository
{
    private readonly CatalogDbContext _db;
    public ProductRepository(CatalogDbContext db) : base(db) => _db = db;

    public new async Task<Application.Domain.Products.Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Application.Domain.Products.Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var n = sku.Trim().ToUpperInvariant();
        return await _db.Products.FirstOrDefaultAsync(p => p.Sku.Value == n, ct);
    }

    public async Task<bool> SkuExistsAsync(string sku, CancellationToken ct = default)
    {
        var n = sku.Trim().ToUpperInvariant();
        return await _db.Products.AnyAsync(p => p.Sku.Value == n, ct);
    }

    public async Task<IReadOnlyList<Application.Domain.Products.Product>> ListAsync(int page, int pageSize, CancellationToken ct = default)
        => await _db.Products.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public async Task<IReadOnlyList<Application.Domain.Products.Product>> FindWithExpiredReservationsAsync(DateTimeOffset utcNow, CancellationToken ct = default)
        => await _db.Products
            .Where(p => p.Reservations.Any(r => r.ExpiresAt <= utcNow))
            .ToListAsync(ct);
}

internal sealed class CatalogUnitOfWork : Application.Ports.Outbound.IUnitOfWork
{
    private readonly EfCoreUnitOfWork<CatalogDbContext> _inner;
    public CatalogUnitOfWork(CatalogDbContext db) => _inner = new EfCoreUnitOfWork<CatalogDbContext>(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<Framework.Results.Result> ExecuteInTransactionAsync(Func<Task<Framework.Results.Result>> action, CancellationToken ct = default)
        => _inner.ExecuteInTransactionAsync(action, ct);
}
